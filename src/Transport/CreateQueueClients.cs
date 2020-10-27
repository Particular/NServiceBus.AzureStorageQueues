namespace NServiceBus.Transport.AzureStorageQueues
{
    using System.Collections.Concurrent;
    using global::Azure.Storage.Queues;

    class CreateQueueClients
    {
        public QueueServiceClient Create(string alias)
        {
            return destinationQueueClients.GetOrAdd(alias, a => BuildClient(a));
        }

        public static QueueServiceClient CreateReceiver(string alias)
        {
            return BuildClient(alias);
        }

        static QueueServiceClient BuildClient(string alias)
        {
            return new QueueServiceClient(alias);
        }

        readonly ConcurrentDictionary<string, QueueServiceClient> destinationQueueClients = new ConcurrentDictionary<string, QueueServiceClient>();
    }
}