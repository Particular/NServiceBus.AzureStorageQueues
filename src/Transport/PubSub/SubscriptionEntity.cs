namespace NServiceBus.Transport.AzureStorageQueues
{
    using System;
    using global::Azure;
    using global::Azure.Data.Tables;

    class SubscriptionEntity : ITableEntity
    {
        public string Topic { get; set; }

        public string Endpoint { get; set; }

        public string Address { get; set; }

        public string PartitionKey
        {
            get => Topic;
            set => Topic = value;
        }

        public string RowKey
        {
            get => Endpoint;
            set => Endpoint = value;
        }

        public DateTimeOffset? Timestamp { get; set; }

        public ETag ETag { get; set; }
    }
}