namespace NServiceBus.Azure.Transports.WindowsAzureStorageQueues
{
    using System;
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.AzureServiceBus;
    using NServiceBus.Extensibility;
    using NServiceBus.Logging;
    using NServiceBus.Transports;

    class PollingDequeueStrategy : IPushMessages, IDisposable
    {
        static readonly ILog Logger = LogManager.GetLogger(typeof(PollingDequeueStrategy));
        static readonly TimeSpan StoppingAllTasksTimeout = TimeSpan.FromSeconds(30);
        static readonly TimeSpan TimeToWaitBeforeTriggering = TimeSpan.FromSeconds(30);
        static readonly TimeSpan NoMessageSleep = TimeSpan.FromMilliseconds(100);

        readonly AzureMessageQueueReceiver messageReceiver;
        CancellationToken cancellationToken;
        CancellationTokenSource cancellationTokenSource;
        RepeatedFailuresOverTimeCircuitBreaker circuitBreaker;
        SemaphoreSlim concurrencyLimiter;

        Task messagePumpTask;
        RepeatedFailuresOverTimeCircuitBreaker peekCircuitBreaker;
        Func<PushContext, Task> pipeline;
        ConcurrentDictionary<Task, Task> runningReceiveTasks;

        public PollingDequeueStrategy(AzureMessageQueueReceiver messageReceiver)
        {
            this.messageReceiver = messageReceiver;
        }

        public void Dispose()
        {
            // Injected
        }

        public Task Init(Func<PushContext, Task> pipe, CriticalError criticalError, PushSettings settings)
        {
            pipeline = pipe;
            circuitBreaker = new RepeatedFailuresOverTimeCircuitBreaker("AzureStoragePollingDequeueStrategy", TimeToWaitBeforeTriggering, ex => criticalError.Raise("Failed to receive message from Azure Storage Queue.", ex));
            peekCircuitBreaker = new RepeatedFailuresOverTimeCircuitBreaker("AzureStoragePollingDequeueStrategy-peek", TimeToWaitBeforeTriggering, ex => criticalError.Raise("Failed to receive message from Azure Storage Queue.", ex));

            //    this.tryProcessMessage = tryProcessMessage;
            //    this.endProcessMessage = endProcessMessage;

            //    addressToPoll = address;
            //    settings = transactionSettings;
            //    transactionOptions = new TransactionOptions
            //    {
            //        IsolationLevel = transactionSettings.IsolationLevel,
            //        Timeout = transactionSettings.TransactionTimeout
            //    };
            // TODO: should these settings be pushed down? What about transaction options

            messageReceiver.Init(settings.InputQueue, false);
            return TaskEx.CompletedTask;
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
                    await circuitBreaker.Failure(ex).ConfigureAwait(false);
                }
            }
        }

        async Task InnerProcessMessages()
        {
            while (!cancellationTokenSource.IsCancellationRequested)
            {
                IncomingMessage message;
                try
                {
                    message = await messageReceiver.Receive(cancellationTokenSource.Token);
                    if (message == null)
                    {
                        await Task.Delay(NoMessageSleep);
                        continue;
                    }

                    peekCircuitBreaker.Success();
                }
                catch (Exception ex)
                {
                    Logger.Warn("Receiving from the queue failed", ex);
                    await peekCircuitBreaker.Failure(ex).ConfigureAwait(false);
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
                        using (var bodyStream = message.BodyStream)
                        {
                            var pushContext = new PushContext(message.MessageId, message.Headers, bodyStream, new TransportTransaction(), cancellationTokenSource, new ContextBag());
                            await pipeline(pushContext).ConfigureAwait(false);
                        }
                        circuitBreaker.Success();
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn("Azure Storage Queue receive operation failed", ex);
                        await circuitBreaker.Failure(ex).ConfigureAwait(false);
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
                }, TaskContinuationOptions.ExecuteSynchronously)
                    .Ignore();
            }
        }
    }
}