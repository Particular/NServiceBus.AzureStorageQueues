namespace NServiceBus.Transport.AzureStorageQueues
{
    using global::Azure.Storage.Blobs;

    class UserBlobServiceClientProvider : IBlobServiceClientProvider
    {
        public UserBlobServiceClientProvider(BlobServiceClient blobServiceClient)
        {
            Client = blobServiceClient;
        }

        public BlobServiceClient Client { get; }
    }
}