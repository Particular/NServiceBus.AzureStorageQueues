namespace NServiceBus.Azure.Transports.WindowsAzureStorageQueues.DelayDelivery
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
        CloudTable timeouts;

        public NativeDelayDelivery(string connectionString, string timeoutTableName)
        {
            timeouts = CloudStorageAccount.Parse(connectionString).CreateCloudTableClient().GetTableReference(timeoutTableName);
        }

        public Task Init()
        {
            return timeouts.CreateIfNotExistsAsync();
        }

        public async Task<bool> ShouldDispatch(UnicastTransportOperation operation, CancellationToken cancellationToken)
        {
            var constraints = operation.DeliveryConstraints;
            var delay = GetVisbilityDelay(constraints);
            if (delay != null)
            {
                if (FirstOrDefault<DiscardIfNotReceivedBefore>(constraints) != null)
                {
                    throw new Exception($"Postponed delivery of messages with TimeToBeReceived set is not supported. Remove the TimeToBeReceived attribute to postpone messages of type '{operation.Message.Headers[Headers.EnclosedMessageTypes]}'.");
                }

                await ScheduleAt(operation, UtcNow + delay.Value, cancellationToken).ConfigureAwait(false);
                return false;
            }

            UnicastTransportOperation operationToSchedule;
            DateTimeOffset scheduleDate;

            if (TryProcessDelayedRetry(operation, out operationToSchedule, out scheduleDate))
            {
                await ScheduleAt(operationToSchedule, scheduleDate, cancellationToken).ConfigureAwait(false);
                return false;
            }

            return true;
        }

        public static DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

        public static StartupCheckResult CheckForInvalidSettings(ReadOnlySettings settings)
        {
            var externalTimeoutManagerAddress = settings.GetOrDefault<string>("NServiceBus.ExternalTimeoutManagerAddress") != null;
            var timeoutManagerFeatureActive = settings.GetOrDefault<FeatureState>(typeof(TimeoutManager).FullName) == FeatureState.Active;
            var timeoutManagerDisabled = (settings.GetOrDefault<DelayedDeliverySettings>()?.TimeoutManagerDisabled).GetValueOrDefault(false);
            
            if (externalTimeoutManagerAddress)
            {
                return StartupCheckResult.Failed("An external timeout manager address cannot be configured because the timeout manager is not being used for delayed delivery.");
            }

            if (!timeoutManagerDisabled && !timeoutManagerFeatureActive)
            {
                return StartupCheckResult.Failed(
                    "The timeout manager is not active, but the transport has not been properly configured for this. " +
                                                 "Use 'EndpointConfiguration.UseTransport<AzureStorageQueueTransport>().DelayedDelivery().DisableTimeoutManager()' to ensure delayed messages can be sent properly.");
            }
            
            return StartupCheckResult.Success;
        }

        static TimeSpan? GetVisbilityDelay(List<DeliveryConstraint> constraints)
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
            string expire;
            var messageHeaders = operation.Message.Headers;
            if (messageHeaders.TryGetValue(TimeoutManagerHeaders.Expire, out expire))
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
            var timeout = new TimeoutEntity
            {
                PartitionKey = TimeoutEntity.GetPartitionKey(date),
                RowKey = $"{TimeoutEntity.GetRawRowKeyPrefix(date)}_{Guid.NewGuid():N}",
            };

            timeout.SetOperation(operation);
            return timeouts.ExecuteAsync(TableOperation.Insert(timeout), cancellationToken);
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