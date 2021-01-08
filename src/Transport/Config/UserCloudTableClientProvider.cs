namespace NServiceBus.Transport.AzureStorageQueues
{
    using Microsoft.Azure.Cosmos.Table;

    class UserCloudTableClientProvider : ICloudTableClientProvider
    {
        public UserCloudTableClientProvider(CloudTableClient cloudTableClient)
        {
            Client = cloudTableClient;
        }

        public CloudTableClient Client { get; }
    }
}