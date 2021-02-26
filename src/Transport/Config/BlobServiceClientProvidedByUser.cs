namespace NServiceBus.Transport.AzureStorageQueues
{
    using global::Azure.Storage.Blobs;

    class BlobServiceClientProvidedByUser : IBlobServiceClientProvider
    {
        public BlobServiceClientProvidedByUser(BlobServiceClient blobServiceClient)
        {
            Client = blobServiceClient;
        }

        public BlobServiceClient Client { get; }
    }
}