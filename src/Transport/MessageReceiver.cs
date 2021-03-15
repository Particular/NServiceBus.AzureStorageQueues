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
            ISubscriptionManager subscriptionManager,
            string receiveAddress,
            string errorQueue,
            Action<string, Exception, CancellationToken> criticalErrorAction,
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
            Subscriptions = subscriptionManager;
            this.receiveAddress = receiveAddress;
            this.errorQueue = errorQueue;
            this.criticalErrorAction = criticalErrorAction;
        }

        public ISubscriptionManager Subscriptions { get; }
        public string Id { get; }

        public Task Initialize(PushRuntimeSettings limitations, OnMessage onMessage, OnError onError, CancellationToken cancellationToken = default)
        {
            this.limitations = limitations;

            Logger.Debug($"Initializing MessageReceiver {Id}");

            receiveStrategy = ReceiveStrategy.BuildReceiveStrategy(onMessage, onError, requiredTransactionMode, criticalErrorAction);

            return azureMessageQueueReceiver.Init(receiveAddress, errorQueue, cancellationToken);
        }

        public Task StartReceive(CancellationToken cancellationToken = default)
        {
            maximumConcurrency = limitations.MaxConcurrency;
            concurrencyLimiter = new SemaphoreSlim(maximumConcurrency, maximumConcurrency);
            messagePumpCancellationTokenSource = new CancellationTokenSource();
            messageProcessingCancellationTokenSource = new CancellationTokenSource();

            circuitBreaker = new RepeatedFailuresOverTimeCircuitBreaker("AzureStorageQueue-MessagePump", TimeToWaitBeforeTriggering, ex => criticalErrorAction("Failed to receive message from Azure Storage Queue.", ex, messageProcessingCancellationTokenSource.Token));

            Logger.DebugFormat($"Starting MessageReceiver {Id} with max concurrency: {0}", maximumConcurrency);

            var receiverConfigurations = MessagePumpHelpers.DetermineReceiverConfiguration(receiveBatchSize, degreeOfReceiveParallelism, maximumConcurrency);

            messagePumpTasks = new Task[receiverConfigurations.Count];

            for (var i = 0; i < receiverConfigurations.Count; i++)
            {
                var backoffStrategy = new BackoffStrategy(peekInterval, maximumWaitTime);
                var batchSizeForReceive = receiverConfigurations[i].BatchSize;
                messagePumpTasks[i] = Task.Run(() => ProcessMessages(batchSizeForReceive, backoffStrategy, messagePumpCancellationTokenSource.Token), cancellationToken);
            }

            return Task.CompletedTask;
        }

        public async Task StopReceive(CancellationToken cancellationToken = default)
        {
            Logger.Debug($"Stopping MessageReceiver {Id}");
            messagePumpCancellationTokenSource?.Cancel();

            using (cancellationToken.Register(() => messageProcessingCancellationTokenSource?.Cancel()))
            {
                while (concurrencyLimiter.CurrentCount != maximumConcurrency)
                {
                    // We are deliberately not forwarding the cancellation token here because
                    // this loop is our way of waiting for all pending messaging operations
                    // to participate in cooperative cancellation or not.
                    // We do not want to rudely abort them because the cancellation token has been cancelled.
                    // This allows us to preserve the same behaviour in v8 as in v7 in that,
                    // if CancellationToken.None is passed to this method,
                    // the method will only return when all in flight messages have been processed.
                    // If, on the other hand, a non-default CancellationToken is passed,
                    // all message processing operations have the opportunity to
                    // participate in cooperative cancellation.
                    // If we ever require a method of stopping the endpoint such that
                    // all message processing is cancelled immediately,
                    // we can provide that as a separate feature.
                    await Task.Delay(50, CancellationToken.None).ConfigureAwait(false);
                }
            }

            concurrencyLimiter.Dispose();

            messageProcessingCancellationTokenSource?.Dispose();
            messagePumpCancellationTokenSource?.Dispose();
        }

        [DebuggerNonUserCode]
        async Task ProcessMessages(int batchSizeForReceive, BackoffStrategy backoffStrategy, CancellationToken pumpCancellationToken)
        {
            if (Logger.IsDebugEnabled)
            {
                Logger.Debug("Processing messages");
            }
            while (!pumpCancellationToken.IsCancellationRequested)
            {
                try
                {
                    await InnerProcessMessages(batchSizeForReceive, backoffStrategy, pumpCancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Logger.Error("Polling Dequeue Strategy failed", ex);
                }
            }
        }

        async Task InnerProcessMessages(int batchSizeForReceive, BackoffStrategy backoffStrategy, CancellationToken pumpCancellationToken)
        {
            var receivedMessages = new List<MessageRetrieved>(batchSizeForReceive);
            if (Logger.IsDebugEnabled)
            {
                Logger.DebugFormat("Fetching {0} messages", batchSizeForReceive);
            }

            while (!pumpCancellationToken.IsCancellationRequested)
            {
                try
                {

                    await azureMessageQueueReceiver.Receive(batchSizeForReceive, receivedMessages, backoffStrategy, pumpCancellationToken).ConfigureAwait(false);
                    circuitBreaker.Success();

                    foreach (var message in receivedMessages)
                    {
                        await concurrencyLimiter.WaitAsync(pumpCancellationToken).ConfigureAwait(false);

                        if (pumpCancellationToken.IsCancellationRequested)
                        {
                            return;
                        }

                        _ = InnerReceive(message, messageProcessingCancellationTokenSource.Token);
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

        async Task InnerReceive(MessageRetrieved retrieved, CancellationToken processingCancellationToken)
        {
            try
            {
                var message = await retrieved.Unwrap(processingCancellationToken).ConfigureAwait(false);
                if (Logger.IsDebugEnabled)
                {
                    Logger.DebugFormat("Unwrapped message ID: '{0}'", message.Id);
                }

                await receiveStrategy.Receive(retrieved, message, processingCancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (processingCancellationToken.IsCancellationRequested)
            {
                // Shutting down
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
        readonly Action<string, Exception, CancellationToken> criticalErrorAction;

        CancellationTokenSource messagePumpCancellationTokenSource;
        CancellationTokenSource messageProcessingCancellationTokenSource;
        RepeatedFailuresOverTimeCircuitBreaker circuitBreaker;
        SemaphoreSlim concurrencyLimiter;

        Task[] messagePumpTasks;
        int maximumConcurrency;
        int? degreeOfReceiveParallelism;
        readonly TransportTransactionMode requiredTransactionMode;
        int? receiveBatchSize;
        static ILog Logger = LogManager.GetLogger<MessageReceiver>();
        static TimeSpan TimeToWaitBeforeTriggering = TimeSpan.FromSeconds(30);
        PushRuntimeSettings limitations;
    }
}