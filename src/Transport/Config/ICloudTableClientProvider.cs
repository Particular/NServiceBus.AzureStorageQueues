namespace NServiceBus.Transport.AzureStorageQueues
{
    using Microsoft.Azure.Cosmos.Table;

    /// <summary>
    /// Provides a <see cref="CloudTableClient"/> via dependency injection. A custom implementation can be registered on the container and will be picked up by the transport.
    /// </summary>
    /// <remarks>This type has been introduced to open them up to enable DI support once the transport seam supports it</remarks>
    interface ICloudTableClientProvider
    {
        /// <summary>
        /// The <see cref="CloudTableClient"/> to use.
        /// </summary>
        CloudTableClient Client { get; }
    }
}