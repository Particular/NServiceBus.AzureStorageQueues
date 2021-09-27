namespace NServiceBus.Transport.AzureStorageQueues
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Features;
    using Microsoft.Azure.Cosmos.Table;
    using Settings;
    using Transport;
    using global::Azure.Storage.Blobs;
    using Logging;

    class NativeDelayDelivery : INativeDelayDelivery
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

        public static DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
        public CloudTable Table { get; private set; }

        public static StartupCheckResult CheckForInvalidSettings(ReadOnlySettings settings)
        {
            var timeoutManagerFeatureActive = settings.IsFeatureActive(typeof(TimeoutManager));
            var timeoutManagerShouldBeEnabled = settings.GetOrDefault<bool>(WellKnownConfigurationKeys.DelayedDelivery.EnableTimeoutManager);

            if (timeoutManagerShouldBeEnabled && !timeoutManagerFeatureActive)
            {
                return StartupCheckResult.Failed(
                    "The timeout manager is not active, but the transport has not been properly configured for this. "
                    + "Use 'EndpointConfiguration.UseTransport<AzureStorageQueueTransport>().DelayedDelivery().DisableTimeoutManager()' to ensure delayed messages can be sent properly.");
            }

            return StartupCheckResult.Success;
        }

        public Task ScheduleDelivery(UnicastTransportOperation operation, DateTimeOffset date, CancellationToken cancellationToken)
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
        readonly string errorQueueAddress;
        readonly bool isAtMostOnce;
        readonly TimeSpan maximumWaitTime;
        readonly TimeSpan peekInterval;
        readonly Func<Dispatcher> dispatcherFactory;
        readonly CloudTableClient cloudTableClient;
        readonly string delayedMessagesTableName;
    }
}
