namespace NServiceBus.Transport.AzureStorageQueues
{
    using global::Azure.Storage.Queues;

    class ConnectionStringQueueServiceClientProvider : IQueueServiceClientProvider
    {
        public ConnectionStringQueueServiceClientProvider(string connectionString)
        {
            ConnectionStringValidator.ThrowIfPremiumEndpointConnectionString(connectionString);

            Client = new QueueServiceClient(connectionString);
        }

        public QueueServiceClient Client { get; }
    }
}