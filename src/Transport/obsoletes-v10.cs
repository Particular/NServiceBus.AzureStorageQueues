#pragma warning disable 1591

namespace NServiceBus
{
    using System;
    using global::Azure.Storage.Queues;

    static partial class AzureStorageTransportExtensions
    {
        /// <summary>
        /// Controls how long messages should be invisible to other callers when receiving messages from the queue
        /// </summary>
        [ObsoleteEx(
            Message = "Configure the transport via the TransportDefinition instance's properties",
            TreatAsErrorFromVersion = "10.0",
            RemoveInVersion = "11.0")]
        public static TransportExtensions<AzureStorageQueueTransport> MessageInvisibleTime(this TransportExtensions<AzureStorageQueueTransport> config, TimeSpan value)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sets the amount of time to add to the time to wait before checking for a new message
        /// </summary>
        [ObsoleteEx(
            Message = "Configure the transport via the TransportDefinition instance's properties",
            TreatAsErrorFromVersion = "10.0",
            RemoveInVersion = "11.0")]
        public static TransportExtensions<AzureStorageQueueTransport> PeekInterval(this TransportExtensions<AzureStorageQueueTransport> config, TimeSpan value)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sets the maximum amount of time, in milliseconds, that the queue will wait before checking for a new message
        /// </summary>
        [ObsoleteEx(
            Message = "Configure the transport via the TransportDefinition instance's properties",
            TreatAsErrorFromVersion = "10.0",
            RemoveInVersion = "11.0")]
        public static TransportExtensions<AzureStorageQueueTransport> MaximumWaitTimeWhenIdle(this TransportExtensions<AzureStorageQueueTransport> config, TimeSpan value)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Registers a queue name sanitizer to apply to queue names not compliant wth Azure Storage Queue naming rules.
        /// <remarks>By default no sanitization is performed.</remarks>
        /// </summary>
        [ObsoleteEx(
            Message = "Configure the transport via the TransportDefinition instance's properties",
            TreatAsErrorFromVersion = "10.0",
            RemoveInVersion = "11.0")]
        public static TransportExtensions<AzureStorageQueueTransport> SanitizeQueueNamesWith(this TransportExtensions<AzureStorageQueueTransport> config, Func<string, string> queueNameSanitizer)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Controls how many messages should be read from the queue at once
        /// </summary>
        [ObsoleteEx(
            Message = "Configure the transport via the TransportDefinition instance's properties",
            TreatAsErrorFromVersion = "10.0",
            RemoveInVersion = "11.0")]
        public static TransportExtensions<AzureStorageQueueTransport> BatchSize(this TransportExtensions<AzureStorageQueueTransport> config, int value)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sets <see cref="QueueServiceClient"/> to be used for messaging operations.
        /// </summary>
        [ObsoleteEx(
            Message = "Configure the transport via the TransportDefinition instance constructor",
            TreatAsErrorFromVersion = "10.0",
            RemoveInVersion = "11.0")]
        public static TransportExtensions<AzureStorageQueueTransport> UseQueueServiceClient(this TransportExtensions<AzureStorageQueueTransport> config, QueueServiceClient queueServiceClient)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sets <see cref="QueueServiceClient"/> to be used for delayed delivery feature.
        /// </summary>
        [ObsoleteEx(
            Message = "Configure the transport via the TransportDefinition instance constructor",
            TreatAsErrorFromVersion = "10.0",
            RemoveInVersion = "11.0")]
        public static TransportExtensions<AzureStorageQueueTransport> UseBlobServiceClient(this TransportExtensions<AzureStorageQueueTransport> config, BlobServiceClient blobServiceClient)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sets <see cref="CloudTableClient"/> to be used for delayed delivery feature.
        /// </summary>
        [ObsoleteEx(
            Message = "Configure the transport via the TransportDefinition instance constructor",
            TreatAsErrorFromVersion = "10.0",
            RemoveInVersion = "11.0")]
        public static TransportExtensions<AzureStorageQueueTransport> UseCloudTableClient(this TransportExtensions<AzureStorageQueueTransport> config, CloudTableClient cloudTableClient)
        {
            throw new NotImplementedException();
        }
    }
}

#pragma warning restore 1591
