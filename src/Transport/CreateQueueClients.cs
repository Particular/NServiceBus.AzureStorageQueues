namespace NServiceBus.Azure.Transports.WindowsAzureStorageQueues
{
    using System.Collections.Concurrent;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Queue;

    class CreateQueueClients
    {
        ConcurrentDictionary<ConnectionString, CloudQueueClient> destinationQueueClients = new ConcurrentDictionary<ConnectionString, CloudQueueClient>();

        public CloudQueueClient Create(ConnectionString connectionString)
        {
            return destinationQueueClients.GetOrAdd(connectionString, BuildClient);
        }

        public CloudQueueClient CreateRecevier(ConnectionString connectionString)
        {
            return BuildClient(connectionString);
        }

        CloudQueueClient BuildClient(ConnectionString connectionString)
        {
            CloudStorageAccount account;
            if (CloudStorageAccount.TryParse(connectionString.Value, out account))
            {
                return account.CreateCloudQueueClient();
            }

            return null;
        }
    }
}