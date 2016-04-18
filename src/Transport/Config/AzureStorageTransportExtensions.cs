namespace NServiceBus
{
    using System;
    using Azure.Transports.WindowsAzureStorageQueues;
    using Azure.Transports.WindowsAzureStorageQueues.Config;
    using Configuration.AdvanceExtensibility;
    using Serialization;

    public static class AzureStorageTransportExtensions
    {
        /// <summary>
        /// Sets the amount of time, in milliseconds, to add to the time to wait before checking for a new message
        /// </summary>
        public static TransportExtensions<AzureStorageQueueTransport> PeekInterval(this TransportExtensions<AzureStorageQueueTransport> config, int value)
        {
            config.GetSettings().Set(WellKnownConfigurationKeys.ReceiverPeekInterval, value);
            return config;
        }

        /// <summary>
        /// Sets the connectionstring to Azure Storage
        /// </summary>
        public static TransportExtensions<AzureStorageQueueTransport> ConnectionString(this TransportExtensions<AzureStorageQueueTransport> config, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentNullException(nameof(value));
            }

            config.ConnectionString(() => value);
            return config;
        }

        /// <summary>
        /// Sets the maximum amount of time, in milliseconds, that the queue will wait before checking for a new message
        /// </summary>
        public static TransportExtensions<AzureStorageQueueTransport> MaximumWaitTimeWhenIdle(this TransportExtensions<AzureStorageQueueTransport> config, int value)
        {
            if (value < 100 || value > 60000)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Value must be between 100ms and 60 seconds.");
            }

            config.GetSettings().Set(WellKnownConfigurationKeys.ReceiverMaximumWaitTimeWhenIdle, value);
            return config;
        }

        /// <summary>
        /// Controls how long messages should be invisible to other callers when receiving messages from the queue
        /// </summary>
        public static TransportExtensions<AzureStorageQueueTransport> MessageInvisibleTime(this TransportExtensions<AzureStorageQueueTransport> config, int value)
        {
            if (value < 1000 || value > 604800000)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Value must be between 1 second and 7 days.");
            }

            config.GetSettings().Set(WellKnownConfigurationKeys.ReceiverMessageInvisibleTime, value);
            return config;
        }

        /// <summary>
        /// Controls how long messages should be invisible to other callers when receiving messages from the queue
        /// </summary>
        public static TransportExtensions<AzureStorageQueueTransport> MessageInvisibleTime(this TransportExtensions<AzureStorageQueueTransport> config, TimeSpan value)
        {
            return config.MessageInvisibleTime((int) value.TotalMilliseconds);
        }

        /// <summary>
        /// Controls how many messages should be read from the queue at once
        /// </summary>
        public static TransportExtensions<AzureStorageQueueTransport> BatchSize(this TransportExtensions<AzureStorageQueueTransport> config, int value)
        {
            if (value < 1 || value > 32)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Batchsize must be between 1 and 32 messages.");
            }

            config.GetSettings().Set(WellKnownConfigurationKeys.ReceiverBatchSize, value);
            return config;
        }

        /// <summary>
        /// Sets a custom serialization for <see cref="MessageWrapper" /> if your configurations uses serialization different
        /// from <see cref="XmlSerializer" /> or <see cref="JsonSerializer" />.
        /// </summary>
        public static TransportExtensions<AzureStorageQueueTransport> SerializeMessageWrapperWith<TSerializationDefinition>(this TransportExtensions<AzureStorageQueueTransport> config)
            where TSerializationDefinition : SerializationDefinition, new()
        {
            config.GetSettings().Set(WellKnownConfigurationKeys.MessageWrapperSerializationDefinition, new TSerializationDefinition());
            return config;
        }

        /// <summary>
        /// Overrides default Md5 shortener for creating queue names with Sha1 shortener.
        /// </summary>
        public static TransportExtensions<AzureStorageQueueTransport> UseSha1ForShortening(this TransportExtensions<AzureStorageQueueTransport> config)
        {
            config.GetSettings().Set(WellKnownConfigurationKeys.Sha1Shortener, true);
            return config;
        }
    }
}