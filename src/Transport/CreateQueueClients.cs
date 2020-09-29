namespace NServiceBus.Transport.AzureStorageQueues
{
    using System.Collections.Concurrent;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Queue;

    class CreateQueueClients
    {
        public CloudQueueClient Create(ConnectionString connectionString)
        {
            return destinationQueueClients.GetOrAdd(connectionString, cs => BuildClient(cs));
        }

        public static CloudQueueClient CreateReceiver(ConnectionString connectionString)
        {
            return BuildClient(connectionString);
        }

        static CloudQueueClient BuildClient(ConnectionString connectionString)
        {
            if (CloudStorageAccount.TryParse(connectionString.Value, out var account))
            {
                return account.CreateCloudQueueClient();
            }

            return null;
        }

        ConcurrentDictionary<ConnectionString, CloudQueueClient> destinationQueueClients = new ConcurrentDictionary<ConnectionString, CloudQueueClient>();
    }
}