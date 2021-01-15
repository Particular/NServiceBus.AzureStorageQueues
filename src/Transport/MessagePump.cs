namespace NServiceBus.Transport.AzureStorageQueues
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using Logging;

    class MessagePump : IPushMessages, IDisposable
    {
        public MessagePump(AzureMessageQueueReceiver messageReceiver, int? degreeOfReceiveParallelism, int? receiveBatchSize, TimeSpan maximumWaitTime, TimeSpan peekInterval)
        {
            this.receiveBatchSize = receiveBatchSize;
            this.degreeOfReceiveParallelism = degreeOfReceiveParallelism;
            this.maximumWaitTime = maximumWaitTime;
            this.peekInterval = peekInterval;
            this.messageReceiver = messageReceiver;
        }

        public void Dispose()
        {
            // Injected
        }

        public Task Init(Func<MessageContext, Task> onMessage, Func<ErrorContext, Task<ErrorHandleResult>> onError, CriticalError criticalError, PushSettings settings)
        {
            Logger.Debug("Initializing the message pump");

            circuitBreaker = new RepeatedFailuresOverTimeCircuitBreaker("AzureStorageQueue-MessagePump", TimeToWaitBeforeTriggering, ex => criticalError.Raise("Failed to receive message from Azure Storage Queue.", ex));
            messageReceiver.PurgeOnStartup = settings.PurgeOnStartup;

            receiveStrategy = ReceiveStrategy.BuildReceiveStrategy(onMessage, onError, settings.RequiredTransactionMode, criticalError);

            return messageReceiver.Init(settings.InputQueue, settings.ErrorQueue);
        }

        public void Start(PushRuntimeSettings limitations)
        {
            maximumConcurrency = limitations.MaxConcurrency;
            concurrencyLimiter = new SemaphoreSlim(maximumConcurrency, maximumConcurrency);
            cancellationTokenSource = new CancellationTokenSource();

            Logger.DebugFormat("Starting the message pump with max concurrency: {0}", maximumConcurrency);

            var receiverConfigurations = MessagePumpHelpers.DetermineReceiverConfiguration(receiveBatchSize, degreeOfReceiveParallelism, maximumConcurrency);

            messagePumpTasks = new Task[receiverConfigurations.Count];

            cancellationToken = cancellationTokenSource.Token;

            for (var i = 0; i < receiverConfigurations.Count; i++)
            {
                var backoffStrategy = new BackoffStrategy(peekInterval, maximumWaitTime);
                var batchSizeForReceive = receiverConfigurations[i].BatchSize;
                messagePumpTasks[i] = Task.Run(() => ProcessMessages(batchSizeForReceive, backoffStrategy), CancellationToken.None);
            }
        }

        public async Task Stop()
        {
            Logger.Debug("Stopping the message pump");
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
                Logger.Error("The message pump failed to stop within the time allowed (30s)");
            }

            concurrencyLimiter.Dispose();
        }

        [DebuggerNonUserCode]
        async Task ProcessMessages(int batchSizeForReceive, BackoffStrategy backoffStrategy)
        {
            if (Logger.IsDebugEnabled)
            {
                Logger.Debug("Processing messages");
            }
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await InnerProcessMessages(batchSizeForReceive, backoffStrategy).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Logger.Error("Polling Dequeue Strategy failed", ex);
                }
            }
        }

        async Task InnerProcessMessages(int batchSizeForReceive, BackoffStrategy backoffStrategy)
        {
            var receivedMessages = new List<MessageRetrieved>(batchSizeForReceive);
            if (Logger.IsDebugEnabled)
            {
                Logger.DebugFormat("Fetching {0} messages", batchSizeForReceive);
            }

            while (!cancellationTokenSource.IsCancellationRequested)
            {
                try
                {

                    await messageReceiver.Receive(batchSizeForReceive, receivedMessages, backoffStrategy, cancellationTokenSource.Token).ConfigureAwait(false);
                    circuitBreaker.Success();

                    foreach (var message in receivedMessages)
                    {
                        await concurrencyLimiter.WaitAsync(cancellationToken).ConfigureAwait(false);

                        if (cancellationTokenSource.IsCancellationRequested)
                        {
                            return;
                        }

                        _ = InnerReceive(message);
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
                finally
                {
                    receivedMessages.Clear();
                }
            }
        }

        async Task InnerReceive(MessageRetrieved retrieved)
        {
            try
            {
                var message = await retrieved.Unwrap().ConfigureAwait(false);
                if (Logger.IsDebugEnabled)
                {
                    Logger.DebugFormat("Unwrapped message ID: '{0}'", message.Id);
                }

                await receiveStrategy.Receive(retrieved, message).ConfigureAwait(false);
            }
            catch (LeaseTimeoutException ex)
            {
                Logger.Warn("Dispatching the message took longer than a visibility timeout. The message will reappear in the queue and will be obtained again.", ex);
            }
            catch (SerializationException ex)
            {
                Logger.Error(ex.Message, ex);
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

        readonly TimeSpan maximumWaitTime;
        readonly TimeSpan peekInterval;

        ReceiveStrategy receiveStrategy;

        AzureMessageQueueReceiver messageReceiver;

        CancellationToken cancellationToken;
        CancellationTokenSource cancellationTokenSource;
        RepeatedFailuresOverTimeCircuitBreaker circuitBreaker;
        SemaphoreSlim concurrencyLimiter;

        Task[] messagePumpTasks;
        int maximumConcurrency;
        int? degreeOfReceiveParallelism;
        int? receiveBatchSize;
        static ILog Logger = LogManager.GetLogger<MessagePump>();
        static TimeSpan StoppingAllTasksTimeout = TimeSpan.FromSeconds(30);
        static TimeSpan TimeToWaitBeforeTriggering = TimeSpan.FromSeconds(30);
    }
}