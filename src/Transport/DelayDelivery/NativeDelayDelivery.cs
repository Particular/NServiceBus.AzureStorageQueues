namespace NServiceBus.Transport.AzureStorageQueues
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using DelayedDelivery;
    using DeliveryConstraints;
    using Features;
    using Microsoft.Azure.Cosmos.Table;
    using Performance.TimeToBeReceived;
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
            bool delayedDeliveryEnabled,
            string errorQueueAddress,
            TransportTransactionMode transactionMode,
            TimeSpan maximumWaitTime,
            TimeSpan peekInterval,
            Func<Dispatcher> dispatcherFactory)
        {
            this.delayedMessagesTableName = delayedMessagesTableName;
            cloudTableClient = cloudTableClientProvider.Client;
            blobServiceClient = blobServiceClientProvider.Client;
            this.delayedDeliveryEnabled = delayedDeliveryEnabled;
            this.errorQueueAddress = errorQueueAddress;
            isAtMostOnce = transactionMode == TransportTransactionMode.None;
            this.maximumWaitTime = maximumWaitTime;
            this.peekInterval = peekInterval;
            this.dispatcherFactory = dispatcherFactory;
        }

        public async Task Start()
        {
            if (delayedDeliveryEnabled)
            {
                Logger.Debug("Starting delayed delivery poller");

                Table = cloudTableClient.GetTableReference(delayedMessagesTableName);
                await Table.CreateIfNotExistsAsync().ConfigureAwait(false);

                nativeDelayedMessagesCancellationSource = new CancellationTokenSource();
                poller = new DelayedMessagesPoller(Table, blobServiceClient, errorQueueAddress, isAtMostOnce, dispatcherFactory(), new BackoffStrategy(peekInterval, maximumWaitTime));
                poller.Start(nativeDelayedMessagesCancellationSource.Token);
            }
        }

        public Task Stop()
        {
            if (delayedDeliveryEnabled)
            {
                Logger.Debug("Stopping delayed delivery poller");

                nativeDelayedMessagesCancellationSource?.Cancel();

                return poller != null ? poller.Stop() : Task.CompletedTask;
            }

            return Task.CompletedTask;
        }

        public async Task<bool> ShouldDispatch(UnicastTransportOperation operation, CancellationToken cancellationToken)
        {
            var constraints = operation.DeliveryConstraints;
            var delay = GetVisibilityDelay(constraints);
            if (delay != null)
            {
                if (delayedDeliveryEnabled == false)
                {
                    throw new Exception("Cannot delay delivery of messages when delayed delivery has been disabled. Remove the 'endpointConfiguration.UseTransport<AzureStorageQueues>.DelayedDelivery().DisableDelayedDelivery()' configuration to re-enable delayed delivery.");
                }

                if (FirstOrDefault<DiscardIfNotReceivedBefore>(constraints) != null)
                {
                    throw new Exception($"Postponed delivery of messages with TimeToBeReceived set is not supported. Remove the TimeToBeReceived attribute to postpone messages of type '{operation.Message.Headers[Headers.EnclosedMessageTypes]}'.");
                }

                await ScheduleAt(operation, UtcNow + delay.Value, cancellationToken).ConfigureAwait(false);
                return false;
            }

            return true;
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

        static TimeSpan? GetVisibilityDelay(List<DeliveryConstraint> constraints)
        {
            var doNotDeliverBefore = FirstOrDefault<DoNotDeliverBefore>(constraints);
            if (doNotDeliverBefore != null)
            {
                return ToNullIfNegative(doNotDeliverBefore.At - UtcNow);
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
        readonly bool delayedDeliveryEnabled;
        readonly string errorQueueAddress;
        readonly bool isAtMostOnce;
        readonly TimeSpan maximumWaitTime;
        readonly TimeSpan peekInterval;
        readonly Func<Dispatcher> dispatcherFactory;
        readonly CloudTableClient cloudTableClient;
        readonly string delayedMessagesTableName;
    }
}
