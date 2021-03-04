namespace NServiceBus.Transport.AzureStorageQueues
{
    using Microsoft.Azure.Cosmos.Table;

    class SubscriptionEntity : TableEntity
    {
        public string Topic
        {
            get => PartitionKey;
            set => PartitionKey = value;
        }

        public string Endpoint
        {
            get => RowKey;
            set => RowKey = value;
        }

        public string Address { get; set; }
    }
}