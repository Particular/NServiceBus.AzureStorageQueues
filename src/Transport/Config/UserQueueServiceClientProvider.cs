namespace NServiceBus.Transport.AzureStorageQueues
{
    using global::Azure.Storage.Queues;

    class UserQueueServiceClientProvider : IQueueServiceClientProvider
    {
        public UserQueueServiceClientProvider(QueueServiceClient queueServiceClient)
        {
            Client = queueServiceClient;
        }

        public QueueServiceClient Client { get; }
    }
}