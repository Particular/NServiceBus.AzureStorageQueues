namespace NServiceBus.Transport.AzureStorageQueues
{
    using global::Azure.Storage.Queues;

    sealed class QueueServiceClientProvidedByUser : IQueueServiceClientProvider
    {
        public QueueServiceClientProvidedByUser(QueueServiceClient queueServiceClient) => Client = queueServiceClient;

        public QueueServiceClient Client { get; }
    }
}