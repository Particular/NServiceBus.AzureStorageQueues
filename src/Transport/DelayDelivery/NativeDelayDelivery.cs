namespace NServiceBus.Azure.Transports.WindowsAzureStorageQueues.DelayDelivery
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using DelayedDelivery;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Table;
    using Transport;

    class NativeDelayDelivery
    {
        static readonly TimeSpan MaxVisibilityDelay = TimeSpan.FromDays(1);
        CloudTable timeouts;

        public NativeDelayDelivery(string connectionString, string timeoutTableName)
        {
            timeouts = CloudStorageAccount.Parse(connectionString).CreateCloudTableClient().GetTableReference(timeoutTableName);
        }

        public Task Init()
        {
            return timeouts.CreateIfNotExistsAsync();
        }

        public async Task<DispatchDecision> ShouldDispatch(UnicastTransportOperation operation)
        {
            var delay = GetVisbilityDelay(operation);
            if (delay == null || delay.Value < MaxVisibilityDelay)
            {
                return new DispatchDecision(true, delay);
            }
            await ScheduleAt(operation, UtcNow + delay.Value).ConfigureAwait(false);
            return new DispatchDecision(false, null);
        }

        public static DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

        static TimeSpan? GetVisbilityDelay(IOutgoingTransportOperation operation)
        {
            var deliveryConstraint = operation.DeliveryConstraints.FirstOrDefault(d => d is DelayedDeliveryConstraint);

            var value = TimeSpan.Zero;
            if (deliveryConstraint != null)
            {
                var delay = deliveryConstraint as DelayDeliveryWith;
                if (delay != null)
                {
                    value = delay.Delay;
                    return value;
                }
                else
                {
                    var exact = deliveryConstraint as DoNotDeliverBefore;
                    if (exact != null)
                    {
                        value = UtcNow - exact.At;
                    }
                }
            }

            return value <= TimeSpan.Zero ? (TimeSpan?)null : value;
        }

        Task ScheduleAt(UnicastTransportOperation operation, DateTimeOffset date)
        {
            var timeout = new TimeoutEntity
            {
                PartitionKey = TimeoutEntity.GetPartitionKey(date),
                RowKey = TimeoutEntity.GetRawRowKeyPrefix(date) + "_" + Guid.NewGuid().ToString("N"),
            };

            timeout.SetOperation(operation);
            return timeouts.ExecuteAsync(TableOperation.Insert(timeout));
        }
    }
}