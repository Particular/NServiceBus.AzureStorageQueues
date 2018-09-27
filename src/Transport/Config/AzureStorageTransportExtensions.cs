namespace NServiceBus
{
    using System;
    using Azure.Transports.WindowsAzureStorageQueues;
    using Configuration.AdvancedExtensibility;
    using Microsoft.WindowsAzure.Storage.Queue;
    using Serialization;
    using Transport.AzureStorageQueues;

    /// <summary>Extension methods for <see cref="AzureStorageQueueTransport"/>.</summary>
    public static partial class AzureStorageTransportExtensions
    {
        /// <summary>
        /// Sets the amount of time to add to the time to wait before checking for a new message
        /// </summary>
        public static TransportExtensions<AzureStorageQueueTransport> PeekInterval(this TransportExtensions<AzureStorageQueueTransport> config, TimeSpan value)
        {
            Guard.AgainstNull(nameof(config), config);
            Guard.AgainstNegativeAndZero(nameof(value), value);
            config.GetSettings().Set(WellKnownConfigurationKeys.ReceiverPeekInterval, value);
            return config;
        }

        /// <summary>
        /// Sets the maximum amount of time, in milliseconds, that the queue will wait before checking for a new message
        /// </summary>
        public static TransportExtensions<AzureStorageQueueTransport> MaximumWaitTimeWhenIdle(this TransportExtensions<AzureStorageQueueTransport> config, TimeSpan value)
        {
            Guard.AgainstNull(nameof(config), config);
            if (value < TimeSpan.FromMilliseconds(100) || value > TimeSpan.FromSeconds(60))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Value must be between 100ms and 60 seconds.");
            }

            config.GetSettings().Set(WellKnownConfigurationKeys.ReceiverMaximumWaitTimeWhenIdle, value);
            return config;
        }

        /// <summary>
        /// Controls how long messages should be invisible to other callers when receiving messages from the queue
        /// </summary>
        public static TransportExtensions<AzureStorageQueueTransport> MessageInvisibleTime(this TransportExtensions<AzureStorageQueueTransport> config, TimeSpan value)
        {
            Guard.AgainstNull(nameof(config), config);
            if (value < TimeSpan.FromSeconds(1) || value > TimeSpan.FromDays(7))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Value must be between 1 second and 7 days.");
            }
            config.GetSettings().Set(WellKnownConfigurationKeys.ReceiverMessageInvisibleTime, value);
            return config;
        }

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
        public static TransportExtensions<AzureStorageQueueTransport> UnwrapMessagesWith(this TransportExtensions<AzureStorageQueueTransport> config, Func<CloudQueueMessage, MessageWrapper> unwrapper)
        {
            Guard.AgainstNull(nameof(config), config);
            Guard.AgainstNull(nameof(unwrapper), unwrapper);
            config.GetSettings().Set<IMessageEnvelopeUnwrapper>(new UserProvidedEnvelopeUnwrapper(unwrapper));
            return config;
        }

        /// <summary>
        /// Registers a queue name sanitizer to apply to queue names not compliant wth Azure Storage Queue naming rules.
        /// <remarks>By default no sanitization is performed.</remarks>
        /// </summary>
        public static TransportExtensions<AzureStorageQueueTransport> SanitizeQueueNamesWith(this TransportExtensions<AzureStorageQueueTransport>config, Func<string, string> queueNameSanitizer)
        {
            Guard.AgainstNull(nameof(config), config);
            Guard.AgainstNull(nameof(queueNameSanitizer), queueNameSanitizer);
            Func<string, string> safeShortener = entityName =>
            {
                try
                {
                    return queueNameSanitizer(entityName);
                }
                catch (Exception exception)
                {
                    throw new Exception("Registered queue name sanitizer threw an exception.", exception);
                }
            };
            config.GetSettings().Set(WellKnownConfigurationKeys.QueueSanitizer, safeShortener);
            return config;
        }

        /// <summary>
        /// Sets the degree of parallelism that should be used to receive messages.
        /// </summary>
        public static TransportExtensions<AzureStorageQueueTransport> DegreeOfReceiveParallelism(this TransportExtensions<AzureStorageQueueTransport> config, int degreeOfReceiveParallelism)
        {
            Guard.AgainstNull(nameof(config), config);
            if (degreeOfReceiveParallelism < 1 || degreeOfReceiveParallelism > MaxDegreeOfReceiveParallelism)
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

        internal const int MaxDegreeOfReceiveParallelism = 32;
    }
}