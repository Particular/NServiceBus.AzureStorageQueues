namespace NServiceBus
{
    using System;
    using System.Text.RegularExpressions;
    using Azure.Transports.WindowsAzureStorageQueues;
    using AzureStorageQueues;
    using AzureStorageQueues.Config;
    using Configuration.AdvanceExtensibility;
    using Microsoft.WindowsAzure.Storage.Queue;
    using Serialization;

    public static class AzureStorageTransportExtensions
    {
        /// <summary>
        /// Sets the amount of time to add to the time to wait before checking for a new message
        /// </summary>
        public static TransportExtensions<AzureStorageQueueTransport> PeekInterval(this TransportExtensions<AzureStorageQueueTransport> config, TimeSpan value)
        {
            config.GetSettings().Set(WellKnownConfigurationKeys.ReceiverPeekInterval, value);
            return config;
        }

        /// <summary>
        /// Sets the maximum amount of time, in milliseconds, that the queue will wait before checking for a new message
        /// </summary>
        public static TransportExtensions<AzureStorageQueueTransport> MaximumWaitTimeWhenIdle(this TransportExtensions<AzureStorageQueueTransport> config, TimeSpan value)
        {
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
        /// Registers a custom unwrapper to convert native messages to <see cref="MessageWrapper" />. This is needed when receiving raw json/xml/etc messages from non NServiceBus endpoints.
        /// </summary>
        public static TransportExtensions<AzureStorageQueueTransport> UnwrapMessagesWith(this TransportExtensions<AzureStorageQueueTransport> config, Func<CloudQueueMessage, MessageWrapper> unwrapper)
        {
            config.GetSettings().Set<IMessageEnvelopeUnwrapper>(new UserProvidedEnvelopeUnwrapper(unwrapper));
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

        /// <summary>
        /// Sets the degree of parallelism that should be used to receive messages.
        /// </summary>
        public static TransportExtensions<AzureStorageQueueTransport> DegreeOfReceiveParallelism(this TransportExtensions<AzureStorageQueueTransport> config, int degreeOfReceiveParallelism)
        {
            if (degreeOfReceiveParallelism < 1 || degreeOfReceiveParallelism > MaxDegreeOfReceiveParallelism)
            {
                throw new ArgumentOutOfRangeException(nameof(degreeOfReceiveParallelism), degreeOfReceiveParallelism, "DegreeOfParallelism must be between 1 and 32.");
            }

            config.GetSettings().Set(WellKnownConfigurationKeys.DegreeOfReceiveParallelism, degreeOfReceiveParallelism);
            return config;
        }

        /// <summary>
        /// Switches transport to use timeouts based on the Azure Storage Queues capabilities.
        /// </summary>
        // TODO: UseNativeTimeouts are internalized till doco and migration arrives.
        internal static TransportExtensions<AzureStorageQueueTransport> UseNativeTimeouts(this TransportExtensions<AzureStorageQueueTransport> config, string timeoutTableName)
        {
            var settings = config.GetSettings();

            if (string.IsNullOrWhiteSpace(timeoutTableName))
            {
                throw new ArgumentException($"{nameof(timeoutTableName)} cannot be null nor empty nor whitespace", nameof(timeoutTableName));
            }

            const string tableNameRegex = "^[A-Za-z][A-Za-z0-9]{2,62}$";
            if (new Regex(tableNameRegex).IsMatch(timeoutTableName) == false)
            {
                throw new ArgumentException($"{nameof(timeoutTableName)} must match the following regular expression '{tableNameRegex}'");
            }

            settings.Set(WellKnownConfigurationKeys.NativeTimeoutsTableName, timeoutTableName);

            return config;
        }

        internal const int MaxDegreeOfReceiveParallelism = 32;
    }
}