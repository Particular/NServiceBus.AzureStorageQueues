namespace NServiceBus.Transport.AzureStorageQueues
{
    using Microsoft.Azure.Cosmos.Table;

    class CloudTableClientByConnectionString : ICloudTableClientProvider
    {
        public CloudTableClientByConnectionString(string connectionString)
        {
            var cloudStorageAccount = CloudStorageAccount.Parse(connectionString);
            Client = cloudStorageAccount.CreateCloudTableClient();
        }

        public CloudTableClient Client { get; }
    }
}