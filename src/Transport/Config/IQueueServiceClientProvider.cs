namespace NServiceBus.Transport.AzureStorageQueues
{
    using global::Azure.Storage.Queues;

    /// <summary>
    /// Provides a <see cref="QueueServiceClient"/> via dependency injection. A custom implementation can be registered on the container and will be picked up by the transport.
    /// </summary>
    /// <remarks>This type has been introduced to open them up to enable DI support once the transport seam supports it</remarks>
    interface IQueueServiceClientProvider
    {
        /// <summary>
        /// The <see cref="QueueServiceClient"/> to use.
        /// </summary>
        QueueServiceClient Client { get; }
    }
}