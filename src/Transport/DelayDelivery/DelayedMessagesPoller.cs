﻿namespace NServiceBus.Transport.AzureStorageQueues
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Logging;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Table;
    using Transport;

    class DelayedMessagesPoller
    {
        const int DelayedMessagesProcessedAtOnce = 50;
        static readonly TimeSpan LeaseLength = TimeSpan.FromSeconds(15);
        static readonly TimeSpan HalfOfLeaseLength = TimeSpan.FromTicks(LeaseLength.Ticks / 2);
        static ILog Logger = LogManager.GetLogger<DelayedMessagesPoller>();

        readonly string connectionString;
        readonly Dispatcher dispatcher;
        readonly BackoffStrategy backoffStrategy;

        CloudTable delayedDeliveryTable;
        LockManager lockManager;
        Task delayedMessagesPollerTask;
        bool isAtMostOnce;
        string errorQueue;

        public DelayedMessagesPoller(CloudTable delayedDeliveryTable, string connectionString, string errorQueue, bool isAtMostOnce, Dispatcher dispatcher, BackoffStrategy backoffStrategy)
        {
            this.errorQueue = errorQueue;
            this.isAtMostOnce = isAtMostOnce;
            this.delayedDeliveryTable = delayedDeliveryTable;
            this.connectionString = connectionString;
            this.dispatcher = dispatcher;
            this.backoffStrategy = backoffStrategy;
        }

        public void Start(CancellationToken cancellationToken)
        {
            var storageAccount = CloudStorageAccount.Parse(connectionString);
            var container = storageAccount.CreateCloudBlobClient().GetContainerReference(delayedDeliveryTable.Name);
            lockManager = new LockManager(container, LeaseLength);

            // No need to pass token to run. to avoid when token is canceled the task changing into
            // the canceled state and when awaited while stopping rethrow the canceled exception
            delayedMessagesPollerTask = Task.Run(() => Poll(cancellationToken), CancellationToken.None);
        }

        public Task Stop()
        {
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
                if (await TryLease(cancellationToken)
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
            // run as there was no messages at all
            return backoffStrategy.OnBatch(0, cancellationToken);
        }

        Task<bool> TryLease(CancellationToken cancellationToken)
        {
            return lockManager.TryLockOrRenew(cancellationToken);
        }

        async Task SpinOnce(CancellationToken cancellationToken)
        {
            var now = NativeDelayDelivery.UtcNow;
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            Logger.DebugFormat("Polling for delayed messages at {0}.", now);

            var query = new TableQuery<DelayedMessageEntity>
            {
                FilterString = $"(PartitionKey le '{DelayedMessageEntity.GetPartitionKey(now)}') and (RowKey le '{DelayedMessageEntity.GetRawRowKeyPrefix(now)}')",
                TakeCount = DelayedMessagesProcessedAtOnce // max batch size
            };

            var delayedMessages = await delayedDeliveryTable.ExecuteQueryAsync(query, DelayedMessagesProcessedAtOnce, cancellationToken)
                .ConfigureAwait(false);

            if (await TryLease(cancellationToken).ConfigureAwait(false) == false)
            {
                return;
            }

            var stopwatch = Stopwatch.StartNew();

            foreach (var delayedMessage in delayedMessages)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                // after half check if the lease is active
                if (stopwatch.Elapsed > HalfOfLeaseLength)
                {
                    if (await TryLease(cancellationToken).ConfigureAwait(false) == false)
                    {
                        return;
                    }
                    stopwatch.Reset();
                }

                try
                {
                    var delete = TableOperation.Delete(delayedMessage);

                    if (isAtMostOnce)
                    {
                        // delete first, then dispatch
                        // TODO: raise an issue with the client library about missing overload accepting operation and cancellation token only
                        await delayedDeliveryTable.ExecuteAsync(delete, null, null, cancellationToken).ConfigureAwait(false);
                        await SafeDispatch(delayedMessage, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        // dispatch first, then delete
                        await SafeDispatch(delayedMessage, cancellationToken).ConfigureAwait(false);
                        // TODO: raise an issue with the client library about missing overload accepting operation and cancellation token only
                        await delayedDeliveryTable.ExecuteAsync(delete, null, null, cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (Exception exception)
                {
                    // just log and move on with the rest
                    Logger.Warn($"Failed at dispatching the delayed message with PartitionKey:'{delayedMessage.PartitionKey}' RowKey: '{delayedMessage.RowKey}' message ID: '{delayedMessage.MessageId}'", exception);
                }
            }

            await backoffStrategy.OnBatch(delayedMessages.Count, cancellationToken).ConfigureAwait(false);
        }

        async Task SafeDispatch(DelayedMessageEntity delayedMessage, CancellationToken cancellationToken)
        {
            var operation = delayedMessage.GetOperation();
            try
            {
                await dispatcher.Send(operation, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // if send fails for any reason
                await dispatcher.Send(CreateOperationForErrorQueue(operation), cancellationToken).ConfigureAwait(false);
            }
        }

        UnicastTransportOperation CreateOperationForErrorQueue(UnicastTransportOperation operation)
        {
            return new UnicastTransportOperation(operation.Message, errorQueue, operation.RequiredDispatchConsistency, operation.DeliveryConstraints);
        }
    }
}