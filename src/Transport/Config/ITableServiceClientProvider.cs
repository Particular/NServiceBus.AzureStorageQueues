namespace NServiceBus.Transport.AzureStorageQueues
{
    using global::Azure.Data.Tables;

    /// <summary>
    /// Provides a <see cref="TableServiceClient"/> via dependency injection. A custom implementation can be registered on the container and will be picked up by the transport.
    /// </summary>
    /// <remarks>This type has been introduced to open them up to enable DI support once the transport seam supports it</remarks>
    interface ITableServiceClientProvider
    {
        /// <summary>
        /// The <see cref="TableServiceClient"/> to use.
        /// </summary>
        TableServiceClient Client { get; }
    }
}