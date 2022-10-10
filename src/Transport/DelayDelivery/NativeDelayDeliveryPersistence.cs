namespace NServiceBus.Transport.AzureStorageQueues
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Azure.Data.Tables;

    class NativeDelayDeliveryPersistence
    {
        public static NativeDelayDeliveryPersistence Disabled() => new();

        NativeDelayDeliveryPersistence(bool enabled = false) => this.enabled = enabled;

        public NativeDelayDeliveryPersistence(TableClient delayedMessageStorageTableClient)
            : this(enabled: true) =>
            this.delayedMessageStorageTableClient = delayedMessageStorageTableClient;

        public static bool IsDelayedMessage(UnicastTransportOperation operation, out DateTimeOffset dueDate)
        {
            var delay = GetDeliveryDelay(operation.Properties);
            if (delay != null)
            {
                if (operation.Properties.DiscardIfNotReceivedBefore != null)
                {
                    throw new Exception($"Delayed delivery of messages with TimeToBeReceived set is not supported. Remove the TimeToBeReceived attribute to postpone messages of type '{operation.Message.Headers[Headers.EnclosedMessageTypes]}'.");
                }

                dueDate = DateTimeOffset.UtcNow + delay.Value;

                return true;
            }

            dueDate = DateTimeOffset.MinValue;
            return false;
        }

        static TimeSpan? GetDeliveryDelay(DispatchProperties properties)
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

        static TimeSpan? ToNullIfNegative(TimeSpan value) =>
            value <= TimeSpan.Zero ? null : value;

        public Task ScheduleAt(UnicastTransportOperation operation, DateTimeOffset date, CancellationToken cancellationToken = default)
        {
            if (!enabled)
            {
                throw new Exception("Native delayed deliveries are not enabled.");
            }

            var delayedMessageEntity = new DelayedMessageEntity
            {
                PartitionKey = DelayedMessageEntity.GetPartitionKey(date),
                RowKey = $"{DelayedMessageEntity.GetRawRowKeyPrefix(date)}_{Guid.NewGuid():N}",
            };

            delayedMessageEntity.SetOperation(operation);
            return delayedMessageStorageTableClient.UpsertEntityAsync(delayedMessageEntity, cancellationToken: cancellationToken);
        }

        readonly TableClient delayedMessageStorageTableClient;
        readonly bool enabled;
    }
}