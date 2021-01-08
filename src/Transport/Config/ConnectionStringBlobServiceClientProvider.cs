namespace NServiceBus.Transport.AzureStorageQueues
{
    using global::Azure.Storage.Blobs;

    class ConnectionStringBlobServiceClientProvider : IBlobServiceClientProvider
    {
        public ConnectionStringBlobServiceClientProvider(string connectionString)
        {
            ConnectionStringValidator.ThrowIfPremiumEndpointConnectionString(connectionString);

            Client = new BlobServiceClient(connectionString);
        }

        public BlobServiceClient Client { get; }
    }
}