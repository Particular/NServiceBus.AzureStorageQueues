namespace NServiceBus.Transport.AzureStorageQueues
{
    using global::Azure.Storage.Queues;

    class QueueServiceClientProvidedByConnectionString : IProvideQueueServiceClient
    {
        public QueueServiceClientProvidedByConnectionString(string connectionString)
        {
            ConnectionStringValidator.ThrowIfPremiumEndpointConnectionString(connectionString);

            Client = new QueueServiceClient(connectionString);
        }

        public QueueServiceClient Client { get; }
    }
}