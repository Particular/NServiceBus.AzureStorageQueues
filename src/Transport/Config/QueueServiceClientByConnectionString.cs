namespace NServiceBus.Transport.AzureStorageQueues
{
    using global::Azure.Storage.Queues;

    class QueueServiceClientByConnectionString : IQueueServiceClientProvider
    {
        public QueueServiceClientByConnectionString(string connectionString)
        {
            ConnectionStringValidator.ThrowIfPremiumEndpointConnectionString(connectionString);

            Client = new QueueServiceClient(connectionString);
        }

        public QueueServiceClient Client { get; }
    }
}