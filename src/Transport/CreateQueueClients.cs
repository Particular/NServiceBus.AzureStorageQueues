namespace NServiceBus.AzureStorageQueues
{
    using System.Collections.Concurrent;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Queue;

    class CreateQueueClients
    {
        public CloudQueueClient Create(ConnectionString connectionString)
        {
            // ReSharper disable once ConvertClosureToMethodGroup
            //return destinationQueueClients.GetOrAdd(connectionString, cs => BuildClient(cs));
            return BuildClient(connectionString);
        }

        public static CloudQueueClient CreateReceiver(ConnectionString connectionString)
        {
            return BuildClient(connectionString);
        }

        static CloudQueueClient BuildClient(ConnectionString connectionString)
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