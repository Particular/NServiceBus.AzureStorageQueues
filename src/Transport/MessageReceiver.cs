namespace NServiceBus.Transport.AzureStorageQueues
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using Logging;

    class MessageReceiver : IMessageReceiver
    {
        public MessageReceiver(string id,
            TransportTransactionMode requiredTransactionMode,
            AzureMessageQueueReceiver azureMessageQueueReceiver,
            string receiveAddress,
            string errorQueue,
            Action<string, Exception> criticalErrorAction,
            int? degreeOfReceiveParallelism,
            int? receiveBatchSize,
            TimeSpan maximumWaitTime,
            TimeSpan peekInterval)
        {
            Id = id;
            this.requiredTransactionMode = requiredTransactionMode;
            this.receiveBatchSize = receiveBatchSize;
            this.degreeOfReceiveParallelism = degreeOfReceiveParallelism;
            this.maximumWaitTime = maximumWaitTime;
            this.peekInterval = peekInterval;
            this.azureMessageQueueReceiver = azureMessageQueueReceiver;
            this.receiveAddress = receiveAddress;
            this.errorQueue = errorQueue;
            this.criticalErrorAction = criticalErrorAction;
        }

        public ISubscriptionManager Subscriptions { get; } = null;
        public string Id { get; }

        public Task Initialize(PushRuntimeSettings limitations, Func<MessageContext, Task> onMessage, Func<ErrorContext, Task<ErrorHandleResult>> onError)
        {
            this.limitations = limitations;

            Logger.Debug($"Initializing MessageReceiver {Id}");

            circuitBreaker = new RepeatedFailuresOverTimeCircuitBreaker("AzureStorageQueue-MessagePump", TimeToWaitBeforeTriggering, ex => criticalErrorAction("Failed to receive message from Azure Storage Queue.", ex));
            receiveStrategy = ReceiveStrategy.BuildReceiveStrategy(onMessage, onError, requiredTransactionMode, criticalErrorAction);

            return azureMessageQueueReceiver.Init(receiveAddress, errorQueue);
        }

        public Task StartReceive()
        {
            maximumConcurrency = limitations.MaxConcurrency;
            concurrencyLimiter = new SemaphoreSlim(maximumConcurrency, maximumConcurrency);
            cancellationTokenSource = new CancellationTokenSource();

            Logger.DebugFormat($"Starting MessageReceiver {Id} with max concurrency: {0}", maximumConcurrency);

            var receiverConfigurations = MessagePumpHelpers.DetermineReceiverConfiguration(receiveBatchSize, degreeOfReceiveParallelism, maximumConcurrency);

            messagePumpTasks = new Task[receiverConfigurations.Count];

            cancellationToken = cancellationTokenSource.Token;

            for (var i = 0; i < receiverConfigurations.Count; i++)
            {
                var backoffStrategy = new BackoffStrategy(peekInterval, maximumWaitTime);
                var batchSizeForReceive = receiverConfigurations[i].BatchSize;
                messagePumpTasks[i] = Task.Run(() => ProcessMessages(batchSizeForReceive, backoffStrategy), CancellationToken.None);
            }

            return Task.CompletedTask;
        }

        public async Task StopReceive()
        {
            Logger.Debug($"Stopping MessageReceiver {Id}");
            cancellationTokenSource.Cancel();

            try
            {
                var tcs = new TaskCompletionSource<bool>();
                using (var timeoutTokenSource = new CancellationTokenSource(StoppingAllTasksTimeout))
                using (timeoutTokenSource.Token.Register(() => tcs.TrySetCanceled())) // ok to have closure alloc here
                {
                    while (concurrencyLimiter.CurrentCount != maximumConcurrency)
                    {
                        await Task.Delay(50, timeoutTokenSource.Token).ConfigureAwait(false);
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

                    await azureMessageQueueReceiver.Receive(batchSizeForReceive, receivedMessages, backoffStrategy, cancellationTokenSource.Token).ConfigureAwait(false);
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

        AzureMessageQueueReceiver azureMessageQueueReceiver;
        readonly string receiveAddress;
        readonly string errorQueue;
        readonly Action<string, Exception> criticalErrorAction;

        CancellationToken cancellationToken;
        CancellationTokenSource cancellationTokenSource;
        RepeatedFailuresOverTimeCircuitBreaker circuitBreaker;
        SemaphoreSlim concurrencyLimiter;

        Task[] messagePumpTasks;
        int maximumConcurrency;
        int? degreeOfReceiveParallelism;
        private readonly TransportTransactionMode requiredTransactionMode;
        int? receiveBatchSize;
        static ILog Logger = LogManager.GetLogger<MessageReceiver>();
        static TimeSpan StoppingAllTasksTimeout = TimeSpan.FromSeconds(30);
        static TimeSpan TimeToWaitBeforeTriggering = TimeSpan.FromSeconds(30);
        private PushRuntimeSettings limitations;
    }
}