namespace NServiceBus.Transport.AzureStorageQueues
{
    using System;
    using System.Diagnostics;
    using System.Runtime.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using Logging;
    using Transport;

    class MessagePump : IPushMessages, IDisposable
    {
        public MessagePump(AzureMessageQueueReceiver messageReceiver, AzureStorageAddressingSettings addressing, int? degreeOfReceiveParallelism, TimeSpan maximumWaitTime, TimeSpan peekInterval)
        {
            this.degreeOfReceiveParallelism = degreeOfReceiveParallelism;
            this.maximumWaitTime = maximumWaitTime;
            this.peekInterval = peekInterval;
            this.messageReceiver = messageReceiver;
            this.addressing = addressing;
        }

        public void Dispose()
        {
            // Injected
        }

        public Task Init(Func<MessageContext, Task> onMessage, Func<ErrorContext, Task<ErrorHandleResult>> onError, CriticalError criticalError, PushSettings settings)
        {
            circuitBreaker = new RepeatedFailuresOverTimeCircuitBreaker("AzureStorageQueue-MessagePump", TimeToWaitBeforeTriggering, ex => criticalError.Raise("Failed to receive message from Azure Storage Queue.", ex));
            messageReceiver.PurgeOnStartup = settings.PurgeOnStartup;

            receiveStrategy = ReceiveStrategy.BuildReceiveStrategy(onMessage, onError, settings.RequiredTransactionMode);

            return messageReceiver.Init(settings.InputQueue, settings.ErrorQueue);
        }

        public void Start(PushRuntimeSettings limitations)
        {
            maximumConcurrency = limitations.MaxConcurrency;
            concurrencyLimiter = new SemaphoreSlim(maximumConcurrency, maximumConcurrency);
            cancellationTokenSource = new CancellationTokenSource();

            if (!degreeOfReceiveParallelism.HasValue)
            {
                degreeOfReceiveParallelism = EstimateDegreeOfReceiveParallelism(maximumConcurrency);
            }

            messagePumpTasks = new Task[degreeOfReceiveParallelism.Value];

            cancellationToken = cancellationTokenSource.Token;

            for (var i = 0; i < degreeOfReceiveParallelism; i++)
            {
                var backoffStrategy = new BackoffStrategy(peekInterval, maximumWaitTime);
                messagePumpTasks[i] = Task.Run(() => ProcessMessages(backoffStrategy), CancellationToken.None);
            }
        }

        public async Task Stop()
        {
            cancellationTokenSource.Cancel();

            try
            {
                var tcs = new TaskCompletionSource<bool>();
                using (var timeoutTokensource = new CancellationTokenSource(StoppingAllTasksTimeout))
                using (timeoutTokensource.Token.Register(() => tcs.TrySetCanceled())) // ok to have closure alloc here
                {
                    while (concurrencyLimiter.CurrentCount != maximumConcurrency)
                    {
                        await Task.Delay(50, timeoutTokensource.Token).ConfigureAwait(false);
                    }

                    await Task.WhenAny(Task.WhenAll(messagePumpTasks), tcs.Task).ConfigureAwait(false);
                    tcs.TrySetResult(true); // if we reach this WhenAll was successful
                }
            }
            catch (OperationCanceledException)
            {
                Logger.Error("The message pump failed to stop with in the time allowed(30s)");
            }

            concurrencyLimiter.Dispose();
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
        async Task ProcessMessages(BackoffStrategy backoffStrategy)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await InnerProcessMessages(backoffStrategy).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Logger.Error("Polling Dequeue Strategy failed", ex);
                }
            }
        }

        async Task InnerProcessMessages(BackoffStrategy backoffStrategy)
        {
            while (!cancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    var retrieved = await messageReceiver.Receive(backoffStrategy, cancellationTokenSource.Token).ConfigureAwait(false);
                    circuitBreaker.Success();

                    foreach (var message in retrieved)
                    {
                        await concurrencyLimiter.WaitAsync(cancellationToken).ConfigureAwait(false);

                        if (cancellationTokenSource.IsCancellationRequested)
                        {
                            return;
                        }

                        InnerReceive(message).Ignore();
                    }
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
            try
            {
                var message = await retrieved.Unwrap().ConfigureAwait(false);
                addressing.ApplyMappingToAliases(message.Headers);

                await receiveStrategy.Receive(retrieved, message).ConfigureAwait(false);
            }
            catch (LeaseTimeoutException ex)
            {
                Logger.Warn("Dispatching the message took longer than a visibility timeout. The message will reappear in the queue and will be obtained again.", ex);
            }
            catch (SerializationException ex)
            {
                Logger.Warn(ex.Message, ex);
            }
            catch (Exception ex)
            {
                Logger.Warn("Azure Storage Queue transport failed pushing a message through pipeline", ex);
            }
            finally
            {
                concurrencyLimiter.Release();
            }
        }

        ReceiveStrategy receiveStrategy;

        AzureStorageAddressingSettings addressing;

        AzureMessageQueueReceiver messageReceiver;

        CancellationToken cancellationToken;
        CancellationTokenSource cancellationTokenSource;
        RepeatedFailuresOverTimeCircuitBreaker circuitBreaker;
        SemaphoreSlim concurrencyLimiter;

        Task[] messagePumpTasks;
        int maximumConcurrency;
        int? degreeOfReceiveParallelism;
        readonly TimeSpan maximumWaitTime;
        readonly TimeSpan peekInterval;
        static ILog Logger = LogManager.GetLogger<MessagePump>();
        static TimeSpan StoppingAllTasksTimeout = TimeSpan.FromSeconds(30);
        static TimeSpan TimeToWaitBeforeTriggering = TimeSpan.FromSeconds(30);
    }
}
