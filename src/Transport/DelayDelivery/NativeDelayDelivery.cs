namespace NServiceBus.Transport.AzureStorageQueues
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using DelayedDelivery;
    using DeliveryConstraints;
    using global::Azure.Storage.Blobs;
    using Logging;
    using Microsoft.Azure.Cosmos.Table;
    using Performance.TimeToBeReceived;
    using Transport;

    class NativeDelayDelivery
    {
        public NativeDelayDelivery(
            IProvideCloudTableClient cloudTableClientProvider,
            IProvideBlobServiceClient blobServiceClientProvider,
            string delayedMessagesTableName,
            string errorQueueAddress,
            TransportTransactionMode transactionMode,
            TimeSpan maximumWaitTime,
            TimeSpan peekInterval,
            Func<Dispatcher> dispatcherFactory)
        {
            this.delayedMessagesTableName = delayedMessagesTableName;
            cloudTableClient = cloudTableClientProvider.Client;
            blobServiceClient = blobServiceClientProvider.Client;
            this.errorQueueAddress = errorQueueAddress;
            isAtMostOnce = transactionMode == TransportTransactionMode.None;
            this.maximumWaitTime = maximumWaitTime;
            this.peekInterval = peekInterval;
            this.dispatcherFactory = dispatcherFactory;
        }

        public async Task Start()
        {
            Logger.Debug("Starting delayed delivery poller");

            Table = cloudTableClient.GetTableReference(delayedMessagesTableName);
            await Table.CreateIfNotExistsAsync().ConfigureAwait(false);

            nativeDelayedMessagesCancellationSource = new CancellationTokenSource();
            poller = new DelayedMessagesPoller(Table, blobServiceClient, errorQueueAddress, isAtMostOnce, dispatcherFactory(), new BackoffStrategy(peekInterval, maximumWaitTime));
            poller.Start(nativeDelayedMessagesCancellationSource.Token);
        }

        public Task Stop()
        {
            Logger.Debug("Stopping delayed delivery poller");
            nativeDelayedMessagesCancellationSource?.Cancel();
            return poller != null ? poller.Stop() : Task.CompletedTask;
        }

        public async Task<bool> ShouldDispatch(UnicastTransportOperation operation, CancellationToken cancellationToken)
        {
            var constraints = operation.DeliveryConstraints;
            var delay = GetDeliveryDelay(constraints);
            if (delay != null)
            {
                if (FirstOrDefault<DiscardIfNotReceivedBefore>(constraints) != null)
                {
                    throw new Exception($"Postponed delivery of messages with TimeToBeReceived set is not supported. Remove the TimeToBeReceived attribute to postpone messages of type '{operation.Message.Headers[Headers.EnclosedMessageTypes]}'.");
                }

                await ScheduleAt(operation, DateTimeOffset.UtcNow + delay.Value, cancellationToken).ConfigureAwait(false);
                return false;
            }

            return true;
        }

        public CloudTable Table { get; private set; }

        static TimeSpan? GetDeliveryDelay(List<DeliveryConstraint> constraints)
        {
            var doNotDeliverBefore = FirstOrDefault<DoNotDeliverBefore>(constraints);
            if (doNotDeliverBefore != null)
            {
                return ToNullIfNegative(doNotDeliverBefore.At - DateTimeOffset.UtcNow);
            }

            var delay = FirstOrDefault<DelayDeliveryWith>(constraints);
            if (delay != null)
            {
                return ToNullIfNegative(delay.Delay);
            }

            return null;
        }

        static TimeSpan? ToNullIfNegative(TimeSpan value)
        {
            return value <= TimeSpan.Zero ? (TimeSpan?)null : value;
        }

        Task ScheduleAt(UnicastTransportOperation operation, DateTimeOffset date, CancellationToken cancellationToken)
        {
            var delayedMessageEntity = new DelayedMessageEntity
            {
                PartitionKey = DelayedMessageEntity.GetPartitionKey(date),
                RowKey = $"{DelayedMessageEntity.GetRawRowKeyPrefix(date)}_{Guid.NewGuid():N}",
            };

            delayedMessageEntity.SetOperation(operation);
            return Table.ExecuteAsync(TableOperation.Insert(delayedMessageEntity), null, null, cancellationToken);
        }

        static TDeliveryConstraint FirstOrDefault<TDeliveryConstraint>(List<DeliveryConstraint> constraints)
            where TDeliveryConstraint : DeliveryConstraint
        {
            if (constraints == null || constraints.Count == 0)
            {
                return null;
            }

            for (var i = 0; i < constraints.Count; i++)
            {
                if (constraints[i] is TDeliveryConstraint c)
                {
                    return c;
                }
            }

            return null;
        }



        static readonly ILog Logger = LogManager.GetLogger<NativeDelayDelivery>();

        DelayedMessagesPoller poller;
        CancellationTokenSource nativeDelayedMessagesCancellationSource;
        readonly BlobServiceClient blobServiceClient;
        readonly string errorQueueAddress;
        readonly bool isAtMostOnce;
        readonly TimeSpan maximumWaitTime;
        readonly TimeSpan peekInterval;
        readonly Func<Dispatcher> dispatcherFactory;
        readonly CloudTableClient cloudTableClient;
        readonly string delayedMessagesTableName;
    }
}
