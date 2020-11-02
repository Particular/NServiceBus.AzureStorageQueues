namespace NServiceBus.Transport.AzureStorageQueues
{
    using global::Azure.Storage.Queues;

    class QueueServiceClientProvidedByConfiguration : IProvideQueueServiceClient
    {
        public QueueServiceClientProvidedByConfiguration(QueueServiceClient queueServiceClient)
        {
            Client = queueServiceClient;
        }

        public QueueServiceClient Client { get; }
    }
}