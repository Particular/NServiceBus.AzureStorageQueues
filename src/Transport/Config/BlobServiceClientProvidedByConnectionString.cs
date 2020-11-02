namespace NServiceBus.Transport.AzureStorageQueues
{
    using global::Azure.Storage.Blobs;

    class BlobServiceClientProvidedByConnectionString : IProvideBlobServiceClient
    {
        public BlobServiceClientProvidedByConnectionString(string connectionString)
        {
            Client = new BlobServiceClient(connectionString);
        }

        public BlobServiceClient Client { get; }
    }
}