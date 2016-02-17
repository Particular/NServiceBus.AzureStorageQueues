namespace NServiceBus.Azure.Transports.WindowsAzureStorageQueues
{
    using System.Collections.Concurrent;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Queue;

    public class CreateQueueClients : ICreateQueueClients
    {
        readonly ConcurrentDictionary<string, CloudQueueClient> destinationQueueClients = new ConcurrentDictionary<string, CloudQueueClient>();

        public CloudQueueClient Create(string connectionStringValue)
        {
            return destinationQueueClients.GetOrAdd(connectionStringValue, BuildClient);
        }

        private CloudQueueClient BuildClient(string connectionString)
        {
            CloudStorageAccount account;
            if (CloudStorageAccount.TryParse(connectionString, out account))
            {
                return account.CreateCloudQueueClient();
            }

            return null;
        }
    }
}