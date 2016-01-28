namespace NServiceBus.Azure.Transports.WindowsAzureStorageQueues
{
    using System.Collections.Concurrent;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Queue;
    using NServiceBus.Settings;

    public class CreateQueueClients : ICreateQueueClients
    {
        readonly ConcurrentDictionary<string, CloudQueueClient> destinationQueueClients = new ConcurrentDictionary<string, CloudQueueClient>();
        readonly DeterminesBestConnectionStringForStorageQueues validation;

        public CreateQueueClients(ReadOnlySettings settings, string defaultConnectionString)
        {
            validation = new DeterminesBestConnectionStringForStorageQueues(settings, defaultConnectionString);
        }

        public CloudQueueClient Create(string connectionString)
        {
            return destinationQueueClients.GetOrAdd(connectionString, s =>
            {
                if (!validation.IsPotentialStorageQueueConnectionString(connectionString))
                {
                    connectionString = validation.Determine();
                }

                CloudQueueClient sendClient = null;
                CloudStorageAccount account;

                if (CloudStorageAccount.TryParse(connectionString, out account))
                {
                    sendClient = account.CreateCloudQueueClient();
                }

                return sendClient;
            });
        }
    }
}