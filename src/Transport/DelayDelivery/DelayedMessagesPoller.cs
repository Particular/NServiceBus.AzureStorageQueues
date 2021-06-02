namespace NServiceBus.Transport.AzureStorageQueues
{
    using System;
    using System.Collections.Generic;
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
        public DelayedMessagesPoller(CloudTable delayedDeliveryTable, BlobServiceClient blobServiceClient, string errorQueueAddress, bool isAtMostOnce, Dispatcher dispatcher, BackoffStrategy backoffStrategy)
        {
            this.errorQueueAddress = errorQueueAddress;
            this.isAtMostOnce = isAtMostOnce;
            this.delayedDeliveryTable = delayedDeliveryTable;
            this.blobServiceClient = blobServiceClient;
            this.dispatcher = dispatcher;
            this.backoffStrategy = backoffStrategy;
        }

        public void Start(CancellationToken cancellationToken = default)
        {
            Logger.Debug("Starting delayed message poller");

            var containerClient = blobServiceClient.GetBlobContainerClient(delayedDeliveryTable.Name);
            var leaseClient = new BlobLeaseClient(containerClient);
            lockManager = new LockManager(containerClient, leaseClient, LeaseLength);

            pollerCancellationTokenSource = new CancellationTokenSource();

            // Task.Run() so the call returns immediately instead of waiting for the first await or return down the call stack
            delayedMessagesPollerTask = Task.Run(() => PollAndSwallowExceptions(pollerCancellationTokenSource.Token), CancellationToken.None);
        }

        public Task Stop(CancellationToken cancellationToken = default)
        {
            Logger.Debug("Stopping delayed message poller");
            pollerCancellationTokenSource.Cancel();
            return delayedMessagesPollerTask;
        }

        async Task PollAndSwallowExceptions(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Poll(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex.IsCausedBy(cancellationToken))
                {
                    // private token, poller is being canceled, log exception in case stack trace is ever needed for debugging
                    Logger.Debug("Operation canceled while stopping delayed messages poller.", ex);
                    break;
                }
                catch (Exception ex)
                {
                    Logger.Warn("Failed to fetch delayed messages from the storage", ex);
                }
            }

            try
            {
                await lockManager.TryRelease(cancellationToken).ConfigureAwait(false);
            }
#pragma warning disable PS0019 // Do not catch Exception without considering OperationCanceledException - handling is same for OCE
            catch
#pragma warning restore PS0019 // Do not catch Exception without considering OperationCanceledException
            {
                // ignored as lease will expire on its own
            }
        }

        async Task Poll(CancellationToken cancellationToken)
        {
            Logger.Debug("Checking for delayed messages");

            if (await lockManager.TryLockOrRenew(cancellationToken)
                .ConfigureAwait(false))
            {
                try
                {
                    await SpinOnce(cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (Exception ex) when (!ex.IsCausedBy(cancellationToken))
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

            Logger.Debug($"Polling for delayed messages at {now}.");

            var query = new TableQuery<DelayedMessageEntity>
            {
                FilterString = $"(PartitionKey le '{DelayedMessageEntity.GetPartitionKey(now)}') and (RowKey le '{DelayedMessageEntity.GetRawRowKeyPrefix(now)}')",
                TakeCount = DelayedMessagesProcessedAtOnce // max batch size
            };

            var delayedMessages = await delayedDeliveryTable.QueryUpTo(query, DelayedMessagesProcessedAtOnce, cancellationToken)
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
                cancellationToken.ThrowIfCancellationRequested();

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
                dispatchOperations.Add(DeleteAndDispatch(delayedMessage, cancellationToken));
            }

            if (delayedMessagesCount > 0)
            {
                await Task.WhenAll(dispatchOperations).ConfigureAwait(false);
            }

            await backoffStrategy.OnBatch(delayedMessagesCount, cancellationToken).ConfigureAwait(false);
        }

        async Task DeleteAndDispatch(DelayedMessageEntity delayedMessage, CancellationToken cancellationToken)
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
            catch (Exception ex) when (!ex.IsCausedBy(cancellationToken))
            {
                // just log and move on with the rest
                Logger.Warn(
                    $"Failed at dispatching the delayed message with PartitionKey:'{delayedMessage.PartitionKey}' RowKey: '{delayedMessage.RowKey}' message ID: '{delayedMessage.MessageId}'",
                    ex);
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
            catch (Exception ex) when (!ex.IsCausedBy(cancellationToken))
            {
                // if send fails for any reason
                Logger.Warn($"Failed to send the delayed message with PartitionKey:'{delayedMessage.PartitionKey}' RowKey: '{delayedMessage.RowKey}' message ID: '{delayedMessage.MessageId}'", ex);
                await dispatcher.Send(CreateOperationForErrorQueue(operation), cancellationToken).ConfigureAwait(false);
            }
        }

        UnicastTransportOperation CreateOperationForErrorQueue(UnicastTransportOperation operation)
        {
            //TODO does this need to set the FailedQ header and the failure reason?
            return new UnicastTransportOperation(operation.Message, errorQueueAddress, new DispatchProperties(), operation.RequiredDispatchConsistency);
        }

        const int DelayedMessagesProcessedAtOnce = 50;

        static readonly TimeSpan LeaseLength = TimeSpan.FromSeconds(15);
        static readonly TimeSpan HalfOfLeaseLength = TimeSpan.FromTicks(LeaseLength.Ticks / 2);
        static readonly ILog Logger = LogManager.GetLogger<DelayedMessagesPoller>();

        readonly BlobServiceClient blobServiceClient;
        readonly Dispatcher dispatcher;
        readonly BackoffStrategy backoffStrategy;
        readonly bool isAtMostOnce;
        readonly string errorQueueAddress;
        readonly CloudTable delayedDeliveryTable;

        LockManager lockManager;
        Task delayedMessagesPollerTask;
        CancellationTokenSource pollerCancellationTokenSource;
    }
}
