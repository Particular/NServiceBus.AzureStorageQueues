namespace NServiceBus
{
    using System;
    using Azure.Transports.WindowsAzureStorageQueues;
    using Configuration.AdvancedExtensibility;
    using global::Azure.Storage.Queues.Models;
    using global::Azure.Storage.Blobs;
    using global::Azure.Storage.Queues;
    using Microsoft.Azure.Cosmos.Table;
    using Serialization;
    using Transport.AzureStorageQueues;

    /// <summary>Extension methods for <see cref="AzureStorageQueueTransport"/>.</summary>
    public static partial class AzureStorageTransportExtensions
    {
        /// <summary>
        /// Controls how many messages should be read from the queue at once
        /// </summary>
        public static TransportExtensions<AzureStorageQueueTransport> BatchSize(this TransportExtensions<AzureStorageQueueTransport> config, int value)
        {
            Guard.AgainstNull(nameof(config), config);
            if (value < 1 || value > 32)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Batchsize must be between 1 and 32 messages.");
            }

            config.GetSettings().Set(WellKnownConfigurationKeys.ReceiverBatchSize, value);
            return config;
        }

        /// <summary>
        /// Sets a custom serialization for <see cref="MessageWrapper" />.
        /// </summary>
        public static TransportExtensions<AzureStorageQueueTransport> SerializeMessageWrapperWith<TSerializationDefinition>(this TransportExtensions<AzureStorageQueueTransport> config)
            where TSerializationDefinition : SerializationDefinition, new()
        {
            Guard.AgainstNull(nameof(config), config);
            config.GetSettings().Set(WellKnownConfigurationKeys.MessageWrapperSerializationDefinition, new TSerializationDefinition());
            return config;
        }

        /// <summary>
        /// Registers a custom unwrapper to convert native messages to <see cref="MessageWrapper" />. This is needed when receiving raw json/xml/etc messages from non NServiceBus endpoints.
        /// </summary>
        public static TransportExtensions<AzureStorageQueueTransport> UnwrapMessagesWith(this TransportExtensions<AzureStorageQueueTransport> config, Func<QueueMessage, MessageWrapper> unwrapper)
        {
            Guard.AgainstNull(nameof(config), config);
            Guard.AgainstNull(nameof(unwrapper), unwrapper);
            config.GetSettings().Set<IMessageEnvelopeUnwrapper>(new UserProvidedEnvelopeUnwrapper(unwrapper));
            return config;
        }

        /// <summary>
        /// Sets the degree of parallelism that should be used to receive messages.
        /// </summary>
        public static TransportExtensions<AzureStorageQueueTransport> DegreeOfReceiveParallelism(this TransportExtensions<AzureStorageQueueTransport> config, int degreeOfReceiveParallelism)
        {
            const int maxDegreeOfReceiveParallelism = 32;

            Guard.AgainstNull(nameof(config), config);
            if (degreeOfReceiveParallelism < 1 || degreeOfReceiveParallelism > maxDegreeOfReceiveParallelism)
            {
                throw new ArgumentOutOfRangeException(nameof(degreeOfReceiveParallelism), degreeOfReceiveParallelism, "DegreeOfParallelism must be between 1 and 32.");
            }

            config.GetSettings().Set(WellKnownConfigurationKeys.DegreeOfReceiveParallelism, degreeOfReceiveParallelism);
            return config;
        }

        /// <summary>
        /// Configures delayed delivery features of this transport.
        /// </summary>
        public static DelayedDeliverySettings DelayedDelivery(this TransportExtensions<AzureStorageQueueTransport> config)
        {
            return new DelayedDeliverySettings(config.GetSettings());
        }

        /// <summary>
        /// Sets <see cref="QueueServiceClient"/> to be used for delayed delivery feature.
        /// </summary>
        public static TransportExtensions<AzureStorageQueueTransport> UseBlobServiceClient(this TransportExtensions<AzureStorageQueueTransport> config, BlobServiceClient blobServiceClient)
        {
            Guard.AgainstNull(nameof(blobServiceClient), blobServiceClient);

            config.GetSettings().Set<IBlobServiceClientProvider>(new UserBlobServiceClientProvider(blobServiceClient));

            return config;
        }

        /// <summary>
        /// Sets <see cref="CloudTableClient"/> to be used for delayed delivery feature.
        /// </summary>
        public static TransportExtensions<AzureStorageQueueTransport> UseCloudTableClient(this TransportExtensions<AzureStorageQueueTransport> config, CloudTableClient cloudTableClient)
        {
            Guard.AgainstNull(nameof(cloudTableClient), cloudTableClient);

            config.GetSettings().Set<ICloudTableClientProvider>(new UserCloudTableClientProvider(cloudTableClient));

            return config;
        }
    }
}