namespace NServiceBus.Azure.Transports.WindowsAzureStorageQueues
{
    using System.Collections.Concurrent;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Queue;

    public class CreateQueueClients : ICreateQueueClients
    {
        private readonly AzureStorageAddressingSettings addressing;
        readonly ConcurrentDictionary<string, CloudQueueClient> destinationQueueClients = new ConcurrentDictionary<string, CloudQueueClient>();

        public CreateQueueClients(AzureStorageAddressingSettings addressing)
        {
            this.addressing = addressing;
        }

        public CloudQueueClient Create(string nameOrConnectionString)
        {
            return destinationQueueClients.GetOrAdd(nameOrConnectionString, BuildClient);
        }

        private CloudQueueClient BuildClient(string nameOrConnectionString)
        {
            CloudStorageAccount account;
            if (CloudStorageAccount.TryParse(nameOrConnectionString, out account))
            {
                return account.CreateCloudQueueClient();
            }

            string connectionString;
            if (addressing.TryMapAccount(nameOrConnectionString, out connectionString))
            {
                if (CloudStorageAccount.TryParse(connectionString, out account))
                {
                    return account.CreateCloudQueueClient();
                }
            }

            return null;
        }
    }
}