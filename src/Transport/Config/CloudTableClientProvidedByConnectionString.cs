namespace NServiceBus.Transport.AzureStorageQueues
{
    using Microsoft.Azure.Cosmos.Table;

    class CloudTableClientProvidedByConnectionString : IProvideCloudTableClient
    {
        public CloudTableClientProvidedByConnectionString(string connectionString)
        {
            var cloudStorageAccount = CloudStorageAccount.Parse(connectionString);
            Client = cloudStorageAccount.CreateCloudTableClient();
        }

        public CloudTableClient Client { get; }
    }
}