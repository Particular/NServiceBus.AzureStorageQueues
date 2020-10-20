namespace NServiceBus.Transport.AzureStorageQueues
{
    using System.Collections.Concurrent;
    using global::Azure.Storage.Queues;

    class CreateQueueClients
    {
        public QueueServiceClient Create(ConnectionString connectionString)
        {
            return destinationQueueClients.GetOrAdd(connectionString, cs => BuildClient(cs));
        }

        public static QueueServiceClient CreateReceiver(ConnectionString connectionString)
        {
            return BuildClient(connectionString);
        }

        static QueueServiceClient BuildClient(ConnectionString connectionString)
        {
            return new QueueServiceClient(connectionString.Value);
        }

        ConcurrentDictionary<ConnectionString, QueueServiceClient> destinationQueueClients = new ConcurrentDictionary<ConnectionString, QueueServiceClient>();
    }
}