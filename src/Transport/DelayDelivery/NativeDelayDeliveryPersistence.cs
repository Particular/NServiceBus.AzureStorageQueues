﻿namespace NServiceBus.Transport.AzureStorageQueues
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Table;

    class NativeDelayDeliveryPersistence
    {
        public static NativeDelayDeliveryPersistence Disabled()
        {
            return new NativeDelayDeliveryPersistence();
        }

        NativeDelayDeliveryPersistence(bool enabled = false)
        {
            this.enabled = enabled;
        }

        public NativeDelayDeliveryPersistence(CloudTable delayedMessageStorageTable)
            : this(enabled: true)
        {
            this.delayedMessageStorageTable = delayedMessageStorageTable;
        }

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

        static TimeSpan? ToNullIfNegative(TimeSpan value)
        {
            return value <= TimeSpan.Zero ? (TimeSpan?)null : value;
        }

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
            return delayedMessageStorageTable.ExecuteAsync(TableOperation.Insert(delayedMessageEntity), null, null, cancellationToken);
        }

        CloudTable delayedMessageStorageTable;
        bool enabled;
    }
}