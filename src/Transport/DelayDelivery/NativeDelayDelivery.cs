namespace NServiceBus.Transport.AzureStorageQueues
{
    using System;
    using System.Collections.Immutable;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Azure.Storage.Blobs;
    using Logging;
    using Microsoft.Azure.Cosmos.Table;
    using Transport;

    class NativeDelayDelivery
    {
        public NativeDelayDelivery(
            ICloudTableClientProvider cloudTableClientProviderProvider,
            IBlobServiceClientProvider blobServiceClientProviderProvider,
            string delayedMessagesTableName,
            ImmutableDictionary<string, string> errorQueueAddresses,
            TransportTransactionMode transactionMode,
            TimeSpan maximumWaitTime,
            TimeSpan peekInterval,
            Func<Dispatcher> dispatcherFactory)
        {
            this.delayedMessagesTableName = delayedMessagesTableName;
            cloudTableClient = cloudTableClientProviderProvider.Client;
            blobServiceClient = blobServiceClientProviderProvider.Client;
            this.errorQueueAddresses = errorQueueAddresses;
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
            poller = new DelayedMessagesPoller(Table, blobServiceClient, errorQueueAddresses, isAtMostOnce, dispatcherFactory(), new BackoffStrategy(peekInterval, maximumWaitTime));
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
            var delay = GetDeliveryDelay(operation.Properties);
            if (delay != null)
            {
                if (operation.Properties.DiscardIfNotReceivedBefore != null)
                {
                    throw new Exception($"Postponed delivery of messages with TimeToBeReceived set is not supported. Remove the TimeToBeReceived attribute to postpone messages of type '{operation.Message.Headers[Headers.EnclosedMessageTypes]}'.");
                }

                await ScheduleAt(operation, DateTimeOffset.UtcNow + delay.Value, cancellationToken).ConfigureAwait(false);
                return false;
            }

            return true;
        }

        public CloudTable Table { get; private set; }

        static TimeSpan? GetDeliveryDelay(OperationProperties properties)
        {
            var doNotDeliverBefore = properties.DoNotDeliverBefore;
            if (doNotDeliverBefore != null)
            {
                return ToNullIfNegative(doNotDeliverBefore.At - DateTimeOffset.UtcNow);
            }

            var delay = properties.DelayDeliveryWith;
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

        static readonly ILog Logger = LogManager.GetLogger<NativeDelayDelivery>();

        DelayedMessagesPoller poller;
        CancellationTokenSource nativeDelayedMessagesCancellationSource;
        readonly BlobServiceClient blobServiceClient;
        readonly ImmutableDictionary<string, string> errorQueueAddresses;
        readonly bool isAtMostOnce;
        readonly TimeSpan maximumWaitTime;
        readonly TimeSpan peekInterval;
        readonly Func<Dispatcher> dispatcherFactory;
        readonly CloudTableClient cloudTableClient;
        readonly string delayedMessagesTableName;
    }
}
