namespace NServiceBus.AzureStorageQueues
{
    using System;
    using System.Threading.Tasks;
    using Extensibility;
    using Microsoft.WindowsAzure.Storage.Table;
    using Transport;

    class TableStorageSubscriptionManager : IManageSubscriptions
    {
        public TableStorageSubscriptionManager(CloudTable cloudTable, string localAddress)
        {
            this.cloudTable = cloudTable;
            this.localAddress = localAddress;
        }

        public Task Subscribe(Type eventType, ContextBag context)
        {
            var partitionKey = eventType.FullName;

            return cloudTable.ExecuteAsync(TableOperation.InsertOrReplace(new Subscription
            {
                PartitionKey = partitionKey,
                RowKey = localAddress
            }));
        }

        public Task Unsubscribe(Type eventType, ContextBag context)
        {
            var partitionKey = eventType.FullName;

            return cloudTable.ExecuteAsync(TableOperation.Delete(new Subscription
            {
                PartitionKey = partitionKey,
                RowKey = localAddress
            }));
        }

        readonly CloudTable cloudTable;
        readonly string localAddress;

        public class Subscription : TableEntity
        {
        }
    }
}