namespace NServiceBus.Azure.Transports.WindowsAzureStorageQueues.DelayDelivery
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using AzureStorageQueues.Config;
    using DelayedDelivery;
    using DeliveryConstraints;
    using Features;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Table;
    using Performance.TimeToBeReceived;
    using Settings;
    using Transport;

    class NativeDelayDelivery
    {
        CloudTable delayedMessagesTable;

        public NativeDelayDelivery(string connectionString, string delayedMessagesTableName)
        {
            delayedMessagesTable = CloudStorageAccount.Parse(connectionString).CreateCloudTableClient().GetTableReference(delayedMessagesTableName);
            // In the constructor to ensure we do not force the calling code to remember to invoke any initialization method.
            // Also, CreateIfNotExistsAsync() returns BEFORE the table is actually ready to be used, causing 404.

            // TODO: original code was calling delayedMessagesTable.CreateIfNotExists(); as it was not affected by the bug the async version had.
            // In case async version still returns before table is created, add a small delay.
            delayedMessagesTable.CreateIfNotExistsAsync().GetAwaiter().GetResult();
        }

        public async Task<bool> ShouldDispatch(UnicastTransportOperation operation, CancellationToken cancellationToken)
        {
            var constraints = operation.DeliveryConstraints;
            var delay = GetVisibilityDelay(constraints);
            if (delay != null)
            {
                if (FirstOrDefault<DiscardIfNotReceivedBefore>(constraints) != null)
                {
                    throw new Exception($"Postponed delivery of messages with TimeToBeReceived set is not supported. Remove the TimeToBeReceived attribute to postpone messages of type '{operation.Message.Headers[Headers.EnclosedMessageTypes]}'.");
                }

                await ScheduleAt(operation, UtcNow + delay.Value, cancellationToken).ConfigureAwait(false);
                return false;
            }

            if (TryProcessDelayedRetry(operation, out var operationToSchedule, out var scheduleDate))
            {
                await ScheduleAt(operationToSchedule, scheduleDate, cancellationToken).ConfigureAwait(false);
                return false;
            }

            return true;
        }

        public static DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
        public CloudTable Table => delayedMessagesTable;

        public static StartupCheckResult CheckForInvalidSettings(ReadOnlySettings settings)
        {
            var externalTimeoutManagerAddress = settings.GetOrDefault<string>("NServiceBus.ExternalTimeoutManagerAddress") != null;
            var timeoutManagerFeatureActive = settings.GetOrDefault<FeatureState>(typeof(TimeoutManager).FullName) == FeatureState.Active;
            var timeoutManagerDisabled = settings.Get<bool>(WellKnownConfigurationKeys.DelayedDelivery.DisableTimeoutManager);

            if (externalTimeoutManagerAddress)
            {
                return StartupCheckResult.Failed("An external timeout manager address cannot be configured because the timeout manager is not being used for delayed delivery.");
            }

            if (!timeoutManagerDisabled && !timeoutManagerFeatureActive)
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

        static bool TryProcessDelayedRetry(IOutgoingTransportOperation operation, out UnicastTransportOperation operationToSchedule, out DateTimeOffset scheduleDate)
        {
            var messageHeaders = operation.Message.Headers;
            if (messageHeaders.TryGetValue(TimeoutManagerHeaders.Expire, out var expire))
            {
                var expiration = DateTimeExtensions.ToUtcDateTime(expire);

                var destination = messageHeaders[TimeoutManagerHeaders.RouteExpiredTimeoutTo];

                messageHeaders.Remove(TimeoutManagerHeaders.Expire);
                messageHeaders.Remove(TimeoutManagerHeaders.RouteExpiredTimeoutTo);

                operationToSchedule = new UnicastTransportOperation(operation.Message, destination, operation.RequiredDispatchConsistency, operation.DeliveryConstraints);

                scheduleDate = expiration;

                return true;
            }

            operationToSchedule = null;
            scheduleDate = default(DateTimeOffset);
            return false;
        }

        Task ScheduleAt(UnicastTransportOperation operation, DateTimeOffset date, CancellationToken cancellationToken)
        {
            var delayedMessageEntity = new DelayedMessageEntity
            {
                PartitionKey = DelayedMessageEntity.GetPartitionKey(date),
                RowKey = $"{DelayedMessageEntity.GetRawRowKeyPrefix(date)}_{Guid.NewGuid():N}",
            };

            delayedMessageEntity.SetOperation(operation);
            return delayedMessagesTable.ExecuteAsync(TableOperation.Insert(delayedMessageEntity), null, null, cancellationToken);
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
                var c = constraints[i] as TDeliveryConstraint;

                if (c != null)
                {
                    return c;
                }
            }

            return null;
        }
    }
}