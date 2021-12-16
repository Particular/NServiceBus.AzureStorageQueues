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
            ReceiveAddress = receiveAddress;
            this.errorQueue = errorQueue;
            this.criticalErrorAction = criticalErrorAction;
        }

        public ISubscriptionManager Subscriptions { get; }
        public string Id { get; }

        public string ReceiveAddress { get; }

        public Task Initialize(PushRuntimeSettings limitations, OnMessage onMessage, OnError onError, CancellationToken cancellationToken = default)
        {
            this.limitations = limitations;

            Logger.Debug($"Initializing MessageReceiver {Id}");

            receiveStrategy = ReceiveStrategy.BuildReceiveStrategy(onMessage, onError, requiredTransactionMode, criticalErrorAction);

            return azureMessageQueueReceiver.Init(ReceiveAddress, errorQueue, cancellationToken);
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

                // Task.Run() so the call returns immediately instead of waiting for the first await or return down the call stack
                messagePumpTasks[i] = Task.Run(() => PumpMessagesAndSwallowExceptions(batchSizeForReceive, backoffStrategy, messagePumpCancellationTokenSource.Token), CancellationToken.None);
            }

            return Task.CompletedTask;
        }

        public async Task ChangeConcurrency(PushRuntimeSettings newLimitations, CancellationToken cancellationToken = default)
        {
            await StopReceive(cancellationToken).ConfigureAwait(false);
            limitations = newLimitations;
            await StartReceive(cancellationToken).ConfigureAwait(false);
        }

        public async Task StopReceive(CancellationToken cancellationToken = default)
        {
            Logger.Debug($"Stopping MessageReceiver {Id}");
            messagePumpCancellationTokenSource?.Cancel();

            using (cancellationToken.Register(() => messageProcessingCancellationTokenSource?.Cancel()))
            {
                await Task.WhenAll(messagePumpTasks).ConfigureAwait(false);

                while (concurrencyLimiter.CurrentCount != maximumConcurrency)
                {
                    // We are deliberately not forwarding the cancellation token here because
                    // this loop is our way of waiting for all pending messaging operations
                    // to participate in cooperative cancellation or not.
                    // We do not want to rudely abort them because the cancellation token has been canceled.
                    // This allows us to preserve the same behaviour in v8 as in v7 in that,
                    // if CancellationToken.None is passed to this method,
                    // the method will only return when all in flight messages have been processed.
                    // If, on the other hand, a non-default CancellationToken is passed,
                    // all message processing operations have the opportunity to
                    // participate in cooperative cancellation.
                    // If we ever require a method of stopping the endpoint such that
                    // all message processing is canceled immediately,
                    // we can provide that as a separate feature.
                    await Task.Delay(50, CancellationToken.None).ConfigureAwait(false);
                }
            }

            concurrencyLimiter.Dispose();

            messageProcessingCancellationTokenSource?.Dispose();
            messagePumpCancellationTokenSource?.Dispose();
            messagePumpTasks = null;
        }

        [DebuggerNonUserCode]
        async Task PumpMessagesAndSwallowExceptions(int batchSizeForReceive, BackoffStrategy backoffStrategy, CancellationToken pumpCancellationToken)
        {
            Logger.Debug("Pumping messages");

            while (!pumpCancellationToken.IsCancellationRequested)
            {
                try
                {
                    await PumpMessages(batchSizeForReceive, backoffStrategy, pumpCancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex.IsCausedBy(pumpCancellationToken))
                {
                    // private token, receiver is being canceled, log exception in case stack trace is ever needed for debugging
                    Logger.Debug("Operation canceled while stopping message receiver.", ex);
                    break;
                }
                catch (Exception ex)
                {
                    Logger.Error("Polling Dequeue Strategy failed", ex);
                }
            }
        }

        async Task PumpMessages(int batchSizeForReceive, BackoffStrategy backoffStrategy, CancellationToken pumpCancellationToken)
        {
            var receivedMessages = new List<MessageRetrieved>(batchSizeForReceive);
            if (Logger.IsDebugEnabled)
            {
                Logger.DebugFormat("Fetching {0} messages", batchSizeForReceive);
            }

            while (true)
            {
                pumpCancellationToken.ThrowIfCancellationRequested();

#pragma warning disable PS0021 // Highlight when a try block passes multiple cancellation tokens - justification:
                // The message processing cancellation token is used for processing,
                // since we only want that to be canceled when the public token passed to Stop() is canceled.
                // The message pump token is being used elsewhere, because we want those operations to be canceled as soon as Stop() is called.
                // The catch clause is correctly filtered on the message pump cancellation token.
                try
#pragma warning restore PS0021 // Highlight when a try block passes multiple cancellation tokens
                {

                    await azureMessageQueueReceiver.Receive(batchSizeForReceive, receivedMessages, backoffStrategy, pumpCancellationToken).ConfigureAwait(false);
                    circuitBreaker.Success();

                    foreach (var message in receivedMessages)
                    {
                        await concurrencyLimiter.WaitAsync(pumpCancellationToken).ConfigureAwait(false);

                        // no Task.Run() here to avoid a closure
                        _ = ProcessMessageSwallowExceptionsAndReleaseConcurrencyLimiter(message, messageProcessingCancellationTokenSource.Token);
                    }
                }
                catch (Exception ex) when (!ex.IsCausedBy(pumpCancellationToken))
                {
                    Logger.Warn("Receiving from the queue failed", ex);
                    await circuitBreaker.Failure(ex, pumpCancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    receivedMessages.Clear();
                }
            }
        }

        async Task ProcessMessageSwallowExceptionsAndReleaseConcurrencyLimiter(MessageRetrieved retrieved, CancellationToken processingCancellationToken)
        {
            try
            {
                var message = await retrieved.Unwrap(processingCancellationToken).ConfigureAwait(false);
                if (Logger.IsDebugEnabled)
                {
                    Logger.DebugFormat("Unwrapped message ID: '{0}'", message.Id);
                }

                await receiveStrategy.Receive(retrieved, message, ReceiveAddress, processingCancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex.IsCausedBy(processingCancellationToken))
            {
                Logger.Debug("Message receiving canceled.", ex);
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

        ReceiveStrategy receiveStrategy;
        CancellationTokenSource messagePumpCancellationTokenSource;
        CancellationTokenSource messageProcessingCancellationTokenSource;
        RepeatedFailuresOverTimeCircuitBreaker circuitBreaker;
        volatile SemaphoreSlim concurrencyLimiter;
        Task[] messagePumpTasks;
        int maximumConcurrency;
        PushRuntimeSettings limitations;

        readonly TimeSpan maximumWaitTime;
        readonly TimeSpan peekInterval;
        readonly AzureMessageQueueReceiver azureMessageQueueReceiver;
        readonly string errorQueue;
        readonly Action<string, Exception, CancellationToken> criticalErrorAction;
        readonly int? degreeOfReceiveParallelism;
        readonly TransportTransactionMode requiredTransactionMode;
        readonly int? receiveBatchSize;

        static readonly ILog Logger = LogManager.GetLogger<MessageReceiver>();
        static readonly TimeSpan TimeToWaitBeforeTriggering = TimeSpan.FromSeconds(30);
    }
}
