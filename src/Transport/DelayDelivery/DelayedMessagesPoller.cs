namespace NServiceBus.Transport.AzureStorageQueues
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Azure.Storage.Blobs;
    using global::Azure.Storage.Blobs.Specialized;
    using Logging;
    using Microsoft.Azure.Cosmos.Table;
    using Transport;

    class DelayedMessagesPoller
    {
        public DelayedMessagesPoller(CloudTable delayedDeliveryTable, BlobServiceClient blobServiceClient, ImmutableDictionary<string, string> errorQueueAddresses, string userDefinedDelayedDeliveryPoisonQueue, bool isAtMostOnce, Dispatcher dispatcher, BackoffStrategy backoffStrategy)
        {
            this.errorQueueAddresses = errorQueueAddresses;
            this.userDefinedDelayedDeliveryPoisonQueue = userDefinedDelayedDeliveryPoisonQueue;
            this.isAtMostOnce = isAtMostOnce;
            this.delayedDeliveryTable = delayedDeliveryTable;
            this.blobServiceClient = blobServiceClient;
            this.dispatcher = dispatcher;
            this.backoffStrategy = backoffStrategy;
        }

        public void Start(CancellationToken cancellationToken)
        {
            Logger.Debug("Starting delayed message poller");

            var containerClient = blobServiceClient.GetBlobContainerClient(delayedDeliveryTable.Name);
            var leaseClient = new BlobLeaseClient(containerClient);
            lockManager = new LockManager(containerClient, leaseClient, LeaseLength);

            // No need to pass token to run. to avoid when token is canceled the task changing into
            // the canceled state and when awaited while stopping rethrow the canceled exception
            delayedMessagesPollerTask = Task.Run(() => Poll(cancellationToken), CancellationToken.None);
        }

        public Task Stop()
        {
            Logger.Debug("Stopping delayed message poller");
            return delayedMessagesPollerTask;
        }

        async Task Poll(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await InnerPoll(cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // graceful shutdown
                }
                catch (Exception ex)
                {
                    Logger.Warn("Failed to fetch delayed messages from the storage", ex);
                }
            }

            try
            {
                await lockManager.TryRelease(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch
            {
                // ignored as lease will expire on its own
            }
        }

        async Task InnerPoll(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (Logger.IsDebugEnabled)
                {
                    Logger.Debug("Checking for delayed messages");
                }

                if (await lockManager.TryLockOrRenew(cancellationToken)
                    .ConfigureAwait(false))
                {
                    try
                    {
                        await SpinOnce(cancellationToken)
                            .ConfigureAwait(false);
                    }
                    catch (Exception ex) when (!(ex is OperationCanceledException))
                    {
                        Logger.Warn("Failed at spinning the poller", ex);
                        await BackoffOnError(cancellationToken)
                            .ConfigureAwait(false);
                    }
                }
                else
                {
                    await BackoffOnError(cancellationToken)
                        .ConfigureAwait(false);
                }
            }
        }

        Task BackoffOnError(CancellationToken cancellationToken)
        {
            if (Logger.IsDebugEnabled)
            {
                Logger.Debug("Backing off from polling for delayed messages");
            }

            // run as there was no messages at all
            return backoffStrategy.OnBatch(0, cancellationToken);
        }

        async Task SpinOnce(CancellationToken cancellationToken)
        {
            var now = DateTimeOffset.UtcNow;
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (Logger.IsDebugEnabled)
            {
                Logger.DebugFormat("Polling for delayed messages at {0}.", now);
            }

            var query = new TableQuery<DelayedMessageEntity>
            {
                FilterString = $"(PartitionKey le '{DelayedMessageEntity.GetPartitionKey(now)}') and (RowKey le '{DelayedMessageEntity.GetRawRowKeyPrefix(now)}')",
                TakeCount = DelayedMessagesProcessedAtOnce // max batch size
            };

            var delayedMessages = await delayedDeliveryTable.ExecuteQueryAsync(query, DelayedMessagesProcessedAtOnce, cancellationToken)
                .ConfigureAwait(false);

            if (await lockManager.TryLockOrRenew(cancellationToken).ConfigureAwait(false) == false)
            {
                return;
            }

            Stopwatch stopwatch = null;
            var delayedMessagesCount = delayedMessages.Count;
            var dispatchOperations = new List<Task>(delayedMessagesCount);
            foreach (var delayedMessage in delayedMessages)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                // only allocate if needed
                stopwatch = stopwatch ?? Stopwatch.StartNew();

                // after half check if the lease is active
                if (stopwatch.Elapsed > HalfOfLeaseLength)
                {
                    if (await lockManager.TryLockOrRenew(cancellationToken).ConfigureAwait(false) == false)
                    {
                        return;
                    }
                    stopwatch.Reset();
                }

                // deliberately not using the dispatchers batching capability because every delayed message dispatch should be independent
                dispatchOperations.Add(DeleteAndDispatch(cancellationToken, delayedMessage));
            }

            if (delayedMessagesCount > 0)
            {
                await Task.WhenAll(dispatchOperations).ConfigureAwait(false);
            }

            await backoffStrategy.OnBatch(delayedMessagesCount, cancellationToken).ConfigureAwait(false);
        }

        async Task DeleteAndDispatch(CancellationToken cancellationToken, DelayedMessageEntity delayedMessage)
        {
            try
            {
                var delete = TableOperation.Delete(delayedMessage);

                if (isAtMostOnce)
                {
                    // delete first, then dispatch
                    await delayedDeliveryTable.ExecuteAsync(delete, cancellationToken).ConfigureAwait(false);
                    await SafeDispatch(delayedMessage, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    // dispatch first, then delete
                    await SafeDispatch(delayedMessage, cancellationToken).ConfigureAwait(false);
                    await delayedDeliveryTable.ExecuteAsync(delete, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception exception)
            {
                // just log and move on with the rest
                Logger.Warn(
                    $"Failed at dispatching the delayed message with PartitionKey:'{delayedMessage.PartitionKey}' RowKey: '{delayedMessage.RowKey}' message ID: '{delayedMessage.MessageId}'",
                    exception);
            }
        }

        async Task SafeDispatch(DelayedMessageEntity delayedMessage, CancellationToken cancellationToken)
        {
            if (Logger.IsDebugEnabled)
            {
                Logger.DebugFormat("Dispatching delayed message ID: '{0}'", delayedMessage.MessageId);
            }

            var operation = delayedMessage.GetOperation();
            try
            {
                await dispatcher.Send(operation, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                // if send fails for any reason
                Logger.Warn($"Failed to send the delayed message with PartitionKey:'{delayedMessage.PartitionKey}' RowKey: '{delayedMessage.RowKey}' message ID: '{delayedMessage.MessageId}'", exception);
                await dispatcher.Send(CreateOperationForErrorQueue(operation), cancellationToken).ConfigureAwait(false);
            }
        }

        UnicastTransportOperation CreateOperationForErrorQueue(UnicastTransportOperation operation)
        {
            if (!errorQueueAddresses.TryGetValue(operation.Destination, out var errorQueue))
            {
                errorQueue = userDefinedDelayedDeliveryPoisonQueue;
            }

            if (string.IsNullOrWhiteSpace(errorQueue))
            {
                throw new Exception($"Cannot find a valid error queue for destination '{operation.Destination}'." +
                    $" Configure a user defined poison queue for delayed deliveries by using the" +
                    $" {nameof(AzureStorageQueueTransport)}.{nameof(AzureStorageQueueTransport.DelayedDelivery)}" +
                    $".{nameof(AzureStorageQueueTransport.DelayedDelivery.DelayedDeliveryPoisonQueue)} property.");
            }

            //TODO does this need to set the FailedQ header too?
            return new UnicastTransportOperation(operation.Message, errorQueue, new DispatchProperties(), operation.RequiredDispatchConsistency);
        }

        const int DelayedMessagesProcessedAtOnce = 50;
        static readonly TimeSpan LeaseLength = TimeSpan.FromSeconds(15);
        static readonly TimeSpan HalfOfLeaseLength = TimeSpan.FromTicks(LeaseLength.Ticks / 2);
        static ILog Logger = LogManager.GetLogger<DelayedMessagesPoller>();

        readonly BlobServiceClient blobServiceClient;
        readonly Dispatcher dispatcher;
        readonly BackoffStrategy backoffStrategy;
        readonly bool isAtMostOnce;
        readonly ImmutableDictionary<string, string> errorQueueAddresses;
        readonly string userDefinedDelayedDeliveryPoisonQueue;
        CloudTable delayedDeliveryTable;
        LockManager lockManager;
        Task delayedMessagesPollerTask;
    }
}
