﻿namespace NServiceBus.Transport.AzureStorageQueues
{
    using global::Azure.Storage.Blobs;

    class BlobServiceClientProvidedByConnectionString : IBlobServiceClientProvider
    {
        public BlobServiceClientProvidedByConnectionString(string connectionString)
        {
            ConnectionStringValidator.ThrowIfPremiumEndpointConnectionString(connectionString);

            Client = new BlobServiceClient(connectionString);
        }

        public BlobServiceClient Client { get; }
    }
}