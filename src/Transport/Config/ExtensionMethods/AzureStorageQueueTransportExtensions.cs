namespace NServiceBus
{
    using System;
    using System.IO;
    using NServiceBus.Azure.Transports.WindowsAzureStorageQueues;
    using NServiceBus.AzureStorageQueues;
    using NServiceBus.Configuration.AdvanceExtensibility;
    using NServiceBus.Serialization;

    public static class AzureStorageQueueTransportExtensions
    {
        /// <summary>
        ///     Sets the amount of time, in milliseconds, to add to the time to wait before checking for a new message
        /// </summary>
        /// <param name="transportExtensions"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static void PeekInterval(this TransportExtensions<AzureStorageQueueTransport> transportExtensions, int value)
        {
            var settings = transportExtensions.GetSettings();
            settings.Set(WellKnownConfigurationKeys.ReceiverPeekInterval, value);
        }

        /// <summary>
        ///     Sets the maximum amount of time, in milliseconds, that the queue will wait before checking for a new message
        /// </summary>
        /// <param name="transportExtensions"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static void MaximumWaitTimeWhenIdle(this TransportExtensions<AzureStorageQueueTransport> transportExtensions, int value)
        {
            if (value < 100 || value > 60000)
                throw new ArgumentOutOfRangeException(nameof(value), value, "Value must be between 100ms and 60 seconds.");

            var settings = transportExtensions.GetSettings();
            settings.Set(WellKnownConfigurationKeys.ReceiverMaximumWaitTimeWhenIdle, value);
        }

        /// <summary>
        ///     Controls how long messages should be invisible to other callers when receiving messages from the queue
        /// </summary>
        /// <param name="transportExtensions"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static void MessageInvisibleTime(this TransportExtensions<AzureStorageQueueTransport> transportExtensions, int value)
        {
            if (value < 1000 || value > 604800000)
                throw new ArgumentOutOfRangeException(nameof(value), value, "Value must be between 1 second and 7 days.");

            var settings = transportExtensions.GetSettings();
            settings.Set(WellKnownConfigurationKeys.ReceiverMessageInvisibleTime, value);
        }

        /// <summary>
        ///     Controls how long messages should be invisible to other callers when receiving messages from the queue
        /// </summary>
        /// <param name="transportExtensions"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static void MessageInvisibleTime(this TransportExtensions<AzureStorageQueueTransport> transportExtensions, TimeSpan value)
        {
            var totalMilliseconds = (int) value.TotalMilliseconds;
            transportExtensions.MessageInvisibleTime(totalMilliseconds);
        }

        /// <summary>
        ///     Controls how many messages should be read from the queue at once
        /// </summary>
        /// <param name="transportExtensions"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static void BatchSize(this TransportExtensions<AzureStorageQueueTransport> transportExtensions, int value)
        {
            if (value < 1 || value > 32)
                throw new ArgumentOutOfRangeException(nameof(value), value, "Batch size must be between 1 and 32 messages.");

            var settings = transportExtensions.GetSettings();
            settings.Set(WellKnownConfigurationKeys.ReceiverBatchSize, value);
        }

        /// <summary>
        ///     Sets a custom serialization for <see cref="MessageWrapper" /> if your configurations uses serialization different
        ///     from <see cref="XmlSerializer" /> or <see cref="JsonSerializer" />.
        /// </summary>
        /// <returns></returns>
        public static void SerializeMessageWrapperWith(this TransportExtensions<AzureStorageQueueTransport> transportExtensions, Action<MessageWrapper, Stream> serialize, Func<Stream, MessageWrapper> deserialize)
        {
            var settings = transportExtensions.GetSettings();
            settings.Set(WellKnownConfigurationKeys.MessageWrapperSerializer, new MessageWrapperSerializer(serialize, deserialize));
        }

        /// <summary>
        ///     Sets a custom serialization for <see cref="MessageWrapper" /> if your configurations uses serialization different
        ///     from <see cref="XmlSerializer" /> or <see cref="JsonSerializer" />.
        /// </summary>
        /// <returns></returns>
        public static void SerializeMessageWrapperWith(this TransportExtensions<AzureStorageQueueTransport> transportExtensions, Func<SerializationDefinition, MessageWrapperSerializer> serializerFactory)
        {
            var settings = transportExtensions.GetSettings();
            settings.Set(WellKnownConfigurationKeys.MessageWrapperSerializerFactory, serializerFactory);
        }
        /// <summary>
        ///     Overrides default Md5 shortener for creating queue names with Sha1 shortener.
        /// </summary>
        /// <returns></returns>
        public static void UseSha1ForShortening(this TransportExtensions<AzureStorageQueueTransport> transportExtensions)
        {
            var settings = transportExtensions.GetSettings();
            settings.Set(WellKnownConfigurationKeys.Sha1Shortener, true);
        }

        public static AzureStorageQueueAccountPartitioningSettings Partitioning(this TransportExtensions<AzureStorageQueueTransport> transportExtensions)
        {
            var settings = transportExtensions.GetSettings();
            return new AzureStorageQueueAccountPartitioningSettings(settings);
        }
    }
}