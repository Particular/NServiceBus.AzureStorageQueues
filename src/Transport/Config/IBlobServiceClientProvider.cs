namespace NServiceBus.Transport.AzureStorageQueues
{
    using global::Azure.Storage.Blobs;

    /// <summary>
    /// Provides a <see cref="BlobServiceClient"/> via dependency injection. A custom implementation can be registered on the container and will be picked up by the transport.
    /// </summary>
    /// <remarks>This type has been introduced to open them up to enable DI support once the transport seam supports it</remarks>
    interface IBlobServiceClientProvider
    {
        /// <summary>
        /// The <see cref="BlobServiceClient"/> to use.
        /// </summary>
        BlobServiceClient Client { get; }
    }
}