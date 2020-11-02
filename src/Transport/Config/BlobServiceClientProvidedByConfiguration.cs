namespace NServiceBus.Transport.AzureStorageQueues
{
    using global::Azure.Storage.Blobs;

    class BlobServiceClientProvidedByConfiguration : IProvideBlobServiceClient
    {
        public BlobServiceClientProvidedByConfiguration(BlobServiceClient blobServiceClient)
        {
            Client = blobServiceClient;
        }

        public BlobServiceClient Client { get; }
    }
}