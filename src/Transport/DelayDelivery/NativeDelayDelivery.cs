namespace NServiceBus.Transport.AzureStorageQueues
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
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
        readonly bool delayedDeliveryDisabled;
        CloudTable delayedMessagesTable;

        public NativeDelayDelivery(string connectionString, string delayedMessagesTableName, bool delayedDeliveryDisabled)
        {
            this.delayedDeliveryDisabled = delayedDeliveryDisabled;
            if (!delayedDeliveryDisabled)
            {
                delayedMessagesTable = CloudStorageAccount.Parse(connectionString).CreateCloudTableClient().GetTableReference(delayedMessagesTableName);
                // In the constructor to ensure we do not force the calling code to remember to invoke any initialization method.
                // Also, CreateIfNotExistsAsync() returns BEFORE the table is actually ready to be used, causing 404.

                // TODO: original code was calling delayedMessagesTable.CreateIfNotExists(); as it was not affected by the bug the async version had.
                // In case async version still returns before table is created, add a small delay.
                delayedMessagesTable.CreateIfNotExistsAsync().GetAwaiter().GetResult();
            }
        }

        public async Task<bool> ShouldDispatch(UnicastTransportOperation operation, CancellationToken cancellationToken)
        {
            var constraints = operation.DeliveryConstraints;
            var delay = GetVisibilityDelay(constraints);
            if (delay != null)
            {
                if (delayedDeliveryDisabled)
                {
                    throw new Exception("Message dispatch has been requested with a delayed delivery. Remove the 'endpointConfiguration.DelayedDelivery().DisableDelayedDelivery()' configuration to re-enable delayed delivery.");
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
        public CloudTable Table => delayedMessagesTable;

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
                if (constraints[i] is TDeliveryConstraint c)
                {
                    return c;
                }
            }

            return null;
        }
    }
}