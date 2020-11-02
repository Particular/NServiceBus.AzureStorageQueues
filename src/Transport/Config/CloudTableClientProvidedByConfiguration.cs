namespace NServiceBus.Transport.AzureStorageQueues
{
    using Microsoft.Azure.Cosmos.Table;

    class CloudTableClientProvidedByConfiguration : IProvideCloudTableClient
    {
        public CloudTableClientProvidedByConfiguration(CloudTableClient cloudTableClient)
        {
            Client = cloudTableClient;
        }

        public CloudTableClient Client { get; }
    }
}