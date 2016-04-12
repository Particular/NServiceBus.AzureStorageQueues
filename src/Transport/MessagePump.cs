namespace NServiceBus.Azure.Transports.WindowsAzureStorageQueues
{
    using System;
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.Extensibility;
    using NServiceBus.Logging;
    using NServiceBus.Transports;
    using NServiceBus.Unicast.Queuing;

    class MessagePump : IPushMessages, IDisposable
    {
        static ILog Logger = LogManager.GetLogger(typeof(MessagePump));
        static TimeSpan StoppingAllTasksTimeout = TimeSpan.FromSeconds(30);
        static TimeSpan TimeToWaitBeforeTriggering = TimeSpan.FromSeconds(30);

        AzureMessageQueueReceiver messageReceiver;
        AzureStorageAddressingSettings addressing;
        bool ackBeforeDispatch;
        CancellationToken cancellationToken;
        CancellationTokenSource cancellationTokenSource;
        RepeatedFailuresOverTimeCircuitBreaker circuitBreaker;
        SemaphoreSlim concurrencyLimiter;

        Task messagePumpTask;
        Func<PushContext, Task> pipeline;
        ConcurrentDictionary<Task, Task> runningReceiveTasks;

        public MessagePump(AzureMessageQueueReceiver messageReceiver, AzureStorageAddressingSettings addressing)
        {
            this.messageReceiver = messageReceiver;
            this.addressing = addressing;
        }

        public void Dispose()
        {
            // Injected
        }

        public async Task Init(Func<PushContext, Task> pipe, CriticalError criticalError, PushSettings settings)
        {
            pipeline = pipe;
            circuitBreaker = new RepeatedFailuresOverTimeCircuitBreaker("AzureStorageQueue-MessagePump", TimeToWaitBeforeTriggering, ex => criticalError.Raise("Failed to receive message from Azure Storage Queue.", ex));
            messageReceiver.PurgeOnStartup = settings.PurgeOnStartup;

            switch (settings.RequiredTransactionMode)
            {
                case TransportTransactionMode.None:
                    ackBeforeDispatch = true;
                    break;
                case TransportTransactionMode.ReceiveOnly:
                    ackBeforeDispatch = false;
                    break;
                default:
                    throw new NotSupportedException($"The TransportTransactionMode {settings.RequiredTransactionMode} is not supported");
            }


            await messageReceiver.Init(settings.InputQueue).ConfigureAwait(false);
        }

        public void Start(PushRuntimeSettings limitations)
        {
            runningReceiveTasks = new ConcurrentDictionary<Task, Task>();
            concurrencyLimiter = new SemaphoreSlim(limitations.MaxConcurrency);
            cancellationTokenSource = new CancellationTokenSource();

            cancellationToken = cancellationTokenSource.Token;
            // ReSharper disable once ConvertClosureToMethodGroup
            // LongRunning is useless combined with async/await
            messagePumpTask = Task.Run(() => ProcessMessages(), CancellationToken.None);
        }

        public async Task Stop()
        {
            cancellationTokenSource.Cancel();

            // ReSharper disable once MethodSupportsCancellation
            var timeoutTask = Task.Delay(StoppingAllTasksTimeout);
            var allTasks = runningReceiveTasks.Values.Concat(new[]
            {
                messagePumpTask
            });
            var finishedTask = await Task.WhenAny(Task.WhenAll(allTasks), timeoutTask).ConfigureAwait(false);

            if (finishedTask.Equals(timeoutTask))
            {
                Logger.Error("The message pump failed to stop with in the time allowed(30s)");
            }

            concurrencyLimiter.Dispose();
            runningReceiveTasks.Clear();
        }

        [DebuggerNonUserCode]
        async Task ProcessMessages()
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await InnerProcessMessages().ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // For graceful shutdown purposes
                }
                catch (Exception ex)
                {
                    Logger.Error("Polling Dequeue Strategy failed", ex);
                }
            }
        }

        async Task InnerProcessMessages()
        {
            while (!cancellationTokenSource.IsCancellationRequested)
            {
                MessageRetrieved retrieved;
                try
                {
                    retrieved = await messageReceiver.Receive(cancellationTokenSource.Token).ConfigureAwait(false);
                    if (retrieved == null)
                    {
                        continue;
                    }

                    if (ackBeforeDispatch)
                    {
                        await retrieved.Ack().ConfigureAwait(false);
                    }

                    circuitBreaker.Success();
                }
                catch (QueueNotFoundException ex)
                {
                    Logger.Error($"The queue '{ex.Queue}' was not found. Create the queue.", ex);
                    await circuitBreaker.Failure(ex).ConfigureAwait(false);
                    continue;
                }
                catch (UnableToDispatchException ex)
                {
                    Logger.Error($"The dispach failed at sending a message to the following queue: '{ex.Queue}'", ex);
                    await circuitBreaker.Failure(ex).ConfigureAwait(false);
                    continue;
                }
                catch (Exception ex)
                {
                    Logger.Warn("Receiving from the queue failed", ex);
                    await circuitBreaker.Failure(ex).ConfigureAwait(false);
                    continue;
                }

                if (cancellationTokenSource.IsCancellationRequested)
                {
                    return;
                }

                await concurrencyLimiter.WaitAsync(cancellationToken).ConfigureAwait(false);

                var tokenSource = new CancellationTokenSource();
                var receiveTask = Task.Run(async () =>
                {
                    try
                    {
                        var message = retrieved.Wrapper;

                        addressing.ApplyMappingToLogicalName(message.Headers);

                        var pushContext = new PushContext(message.Id, message.Headers, new MemoryStream(message.Body), new TransportTransaction(), tokenSource, new ContextBag());
                        await pipeline(pushContext).ConfigureAwait(false);

                        if (ackBeforeDispatch == false)
                        {
                            if (tokenSource.IsCancellationRequested == false)
                            {
                                await retrieved.Ack().ConfigureAwait(false);
                            }
                            else
                            {
                                await retrieved.Nack().ConfigureAwait(false);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        await retrieved.Nack().ConfigureAwait(false);
                        Logger.Warn("Azure Storage Queue transport failed pushing a message through pipeline", ex);
                    }
                    finally
                    {
                        concurrencyLimiter.Release();
                    }
                }, tokenSource.Token).ContinueWith(t => tokenSource.Dispose());

                runningReceiveTasks.TryAdd(receiveTask, receiveTask);

                // We insert the original task into the runningReceiveTasks because we want to await the completion
                // of the running receives. ExecuteSynchronously is a request to execute the continuation as part of
                // the transition of the antecedents completion phase. This means in most of the cases the continuation
                // will be executed during this transition and the antecedent task goes into the completion state only
                // after the continuation is executed. This is not always the case. When the TPL thread handling the
                // antecedent task is aborted the continuation will be scheduled. But in this case we don't need to await
                // the continuation to complete because only really care about the receive operations. The final operation
                // when shutting down is a clear of the running tasks anyway.
                receiveTask.ContinueWith(t =>
                {
                    Task toBeRemoved;
                    runningReceiveTasks.TryRemove(t, out toBeRemoved);
                }, TaskContinuationOptions.ExecuteSynchronously).Ignore();
            }
        }
    }
}