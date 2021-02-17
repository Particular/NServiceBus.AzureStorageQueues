namespace NServiceBus.Transport.AzureStorageQueues
{
    using Microsoft.Azure.Cosmos.Table;

    class CloudTableClientProvidedByUser : ICloudTableClientProvider
    {
        public CloudTableClientProvidedByUser(CloudTableClient cloudTableClient)
        {
            Client = cloudTableClient;
        }

        public CloudTableClient Client { get; }
    }
}