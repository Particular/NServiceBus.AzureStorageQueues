namespace NServiceBus.Azure.Transports.WindowsAzureStorageQueues
{
    using System;
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Extensibility;
    using Logging;
    using NServiceBus.Transports;

    class MessagePump : IPushMessages, IDisposable
    {
        public MessagePump(AzureMessageQueueReceiver messageReceiver, AzureStorageAddressingSettings addressing, int? degreeOfReceiveParallelism)
        {
            this.degreeOfReceiveParallelism = degreeOfReceiveParallelism;
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

            if (!degreeOfReceiveParallelism.HasValue)
            {
                degreeOfReceiveParallelism = EstimateDegreeOfReceiveParallelism(limitations.MaxConcurrency);
            }

            messagePumpTasks = new Task[degreeOfReceiveParallelism.Value];

            cancellationToken = cancellationTokenSource.Token;

            for (var i = 0; i < degreeOfReceiveParallelism; i++)
            {
                messagePumpTasks[i] = Task.Run(ProcessMessages, CancellationToken.None);
            }
        }

        public async Task Stop()
        {
            cancellationTokenSource.Cancel();

            // ReSharper disable once MethodSupportsCancellation
            var timeoutTask = Task.Delay(StoppingAllTasksTimeout);
            var allTasks = runningReceiveTasks.Values.Concat(messagePumpTasks);
            var finishedTask = await Task.WhenAny(Task.WhenAll(allTasks), timeoutTask).ConfigureAwait(false);

            if (finishedTask.Equals(timeoutTask))
            {
                Logger.Error("The message pump failed to stop with in the time allowed(30s)");
            }

            concurrencyLimiter.Dispose();
            runningReceiveTasks.Clear();
        }

        static int EstimateDegreeOfReceiveParallelism(int maxConcurrency)
        {
            /*
             * Degree of parallelism = square root of MaxConcurrency (rounded)
             *  (concurrency 1 = 1, concurrency 10 = 3, concurrency 20 = 4, concurrency 50 = 7, concurrency 100 [default] = 10, concurrency 200 = 14, concurrency 1000 = 32)
             */
            return Math.Min(Convert.ToInt32(Math.Round(Math.Sqrt(Convert.ToDouble(maxConcurrency)))), AzureStorageTransportExtensions.MaxDegreeOfReceiveParallelism);
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
                try
                {
                    var retrieved = await messageReceiver.Receive(cancellationTokenSource.Token).ConfigureAwait(false);

                    foreach (var message in retrieved)
                    {
                        await concurrencyLimiter.WaitAsync(cancellationToken).ConfigureAwait(false);

                        if (cancellationTokenSource.IsCancellationRequested)
                        {
                            return;
                        }

                        var receiveTask = InnerReceive(message);

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

                    circuitBreaker.Success();
                }
                catch (OperationCanceledException)
                {
                    // For graceful shutdown purposes
                    return;
                }
                catch (Exception ex)
                {
                    Logger.Warn("Receiving from the queue failed", ex);
                    await circuitBreaker.Failure(ex).ConfigureAwait(false);
                }
            }
        }

        async Task InnerReceive(MessageRetrieved retrieved)
        {
            using (var tokenSource = new CancellationTokenSource())
            {
                try
                {
                    if (ackBeforeDispatch)
                    {
                        await retrieved.Ack().ConfigureAwait(false);
                    }

                    var message = retrieved.Unwrap();
                    addressing.ApplyMappingToLogicalName(message.Headers);

                    using (var memoryStream = new MemoryStream(message.Body))
                    {
                        var pushContext = new PushContext(message.Id, message.Headers, memoryStream, new TransportTransaction(), tokenSource, new ContextBag());
                        await pipeline(pushContext).ConfigureAwait(false);
                    }

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
                    Logger.Warn("Azure Storage Queue transport failed pushing a message through pipeline", ex);
                    await circuitBreaker.Failure(ex).ConfigureAwait(false);
                    await retrieved.Nack().ConfigureAwait(false);
                }
                finally
                {
                    concurrencyLimiter.Release();
                }
            }
        }

        AzureStorageAddressingSettings addressing;

        AzureMessageQueueReceiver messageReceiver;

        bool ackBeforeDispatch;
        CancellationToken cancellationToken;
        CancellationTokenSource cancellationTokenSource;
        RepeatedFailuresOverTimeCircuitBreaker circuitBreaker;
        SemaphoreSlim concurrencyLimiter;

        Task[] messagePumpTasks;
        Func<PushContext, Task> pipeline;
        ConcurrentDictionary<Task, Task> runningReceiveTasks;
        int? degreeOfReceiveParallelism;
        static ILog Logger = LogManager.GetLogger(typeof(MessagePump));
        static TimeSpan StoppingAllTasksTimeout = TimeSpan.FromSeconds(30);
        static TimeSpan TimeToWaitBeforeTriggering = TimeSpan.FromSeconds(30);
    }
}