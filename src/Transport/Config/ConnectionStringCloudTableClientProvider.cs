namespace NServiceBus.Transport.AzureStorageQueues
{
    using Microsoft.Azure.Cosmos.Table;

    class ConnectionStringCloudTableClientProvider : ICloudTableClientProvider
    {
        public ConnectionStringCloudTableClientProvider(string connectionString)
        {
            var cloudStorageAccount = CloudStorageAccount.Parse(connectionString);
            Client = cloudStorageAccount.CreateCloudTableClient();
        }

        public CloudTableClient Client { get; }
    }
}