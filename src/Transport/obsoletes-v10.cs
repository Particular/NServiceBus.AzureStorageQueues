#pragma warning disable CS0618 // Type or member is obsolete

namespace NServiceBus
{
    using System;
    using global::Azure.Storage.Blobs;
    using global::Azure.Storage.Queues;
    using global::Azure.Storage.Queues.Models;
    using Microsoft.Azure.Cosmos.Table;
    using Azure.Transports.WindowsAzureStorageQueues;
    using Configuration.AdvancedExtensibility;
    using Serialization;
    using Settings;

    /// <summary>
    /// Provides support for <see cref="UseTransport{T}"/> transport APIs.
    /// </summary>
    public static class AzureStorageQueueTransportApiExtensions
    {
        /// <summary>
        /// Configures NServiceBus to use the given transport.
        /// </summary>
        [ObsoleteEx(
            TreatAsErrorFromVersion = "12.0",
            RemoveInVersion = "13.0",
            ReplacementTypeOrMember = "EndpointConfiguration.UseTransport(TransportDefinition)")]
        public static AzureStorageQueueTransportLegacySettings UseTransport<T>(this EndpointConfiguration config, string connectionString)
            where T : AzureStorageQueueTransport
        {
            var transport = new AzureStorageQueueTransport(connectionString);
            var routing = config.UseTransport(transport);
            var settings = new AzureStorageQueueTransportLegacySettings(transport, routing);

            return settings;
        }
    }


    /// <summary>
    /// AzureStorageQueueTransport transport configuration settings.
    /// </summary>
    [ObsoleteEx(
        Message = "Configure the transport via the AzureStorageQueueTransport properties",
        TreatAsErrorFromVersion = "12.0",
        RemoveInVersion = "13.0")]
    public class AzureStorageQueueTransportLegacySettings : TransportSettings<AzureStorageQueueTransport>
    {
        internal AzureStorageQueueTransportLegacySettings(AzureStorageQueueTransport transport, RoutingSettings<AzureStorageQueueTransport> routing)
            : base(transport, routing)
        {
        }

        internal AzureStorageQueueTransport AsqTransport => Transport;

        /// <summary>
        /// Controls how long messages should be invisible to other callers when receiving messages from the queue
        /// </summary>
        [ObsoleteEx(
            Message = "Configure the transport via the AzureStorageQueueTransport MessageInvisibleTime property",
            TreatAsErrorFromVersion = "12.0",
            RemoveInVersion = "13.0")]
        public TransportSettings<AzureStorageQueueTransport> MessageInvisibleTime(TimeSpan value)
        {
            Transport.MessageInvisibleTime = value;
            return this;
        }

        /// <summary>
        /// Sets the amount of time to add to the time to wait before checking for a new message
        /// </summary>
        [ObsoleteEx(
            Message = "Configure the transport via the AzureStorageQueueTransport PeekInterval property",
            TreatAsErrorFromVersion = "12.0",
            RemoveInVersion = "13.0")]
        public TransportSettings<AzureStorageQueueTransport> PeekInterval(TimeSpan value)
        {
            Transport.PeekInterval = value;
            return this;
        }

        /// <summary>
        /// Sets the maximum amount of time, in milliseconds, that the queue will wait before checking for a new message
        /// </summary>
        [ObsoleteEx(
            Message = "Configure the transport via the AzureStorageQueueTransport MaximumWaitTimeWhenIdle property",
            TreatAsErrorFromVersion = "12.0",
            RemoveInVersion = "13.0")]
        public TransportSettings<AzureStorageQueueTransport> MaximumWaitTimeWhenIdle(TimeSpan value)
        {
            Transport.MaximumWaitTimeWhenIdle = value;
            return this;
        }

        /// <summary>
        /// Registers a queue name sanitizer to apply to queue names not compliant wth Azure Storage Queue naming rules.
        /// <remarks>By default no sanitization is performed.</remarks>
        /// </summary>
        [ObsoleteEx(
            Message = "Configure the transport via the AzureStorageQueueTransport QueueNameSanitizer property",
            TreatAsErrorFromVersion = "12.0",
            RemoveInVersion = "13.0")]
        public TransportSettings<AzureStorageQueueTransport> SanitizeQueueNamesWith(Func<string, string> queueNameSanitizer)
        {
            Transport.QueueNameSanitizer = queueNameSanitizer;
            return this;
        }

        /// <summary>
        /// Controls how many messages should be read from the queue at once
        /// </summary>
        [ObsoleteEx(
            Message = "Configure the transport via the AzureStorageQueueTransport ReceiverBatchSize property",
            TreatAsErrorFromVersion = "12.0",
            RemoveInVersion = "13.0")]
        public TransportSettings<AzureStorageQueueTransport> BatchSize(int value)
        {
            Transport.ReceiverBatchSize = value;
            return this;
        }

        /// <summary>
        /// Sets the degree of parallelism that should be used to receive messages.
        /// </summary>
        [ObsoleteEx(
            Message = "Configure the transport via the AzureStorageQueueTransport DegreeOfReceiveParallelism property",
            TreatAsErrorFromVersion = "12.0",
            RemoveInVersion = "13.0")]
        public TransportSettings<AzureStorageQueueTransport> DegreeOfReceiveParallelism(int degreeOfReceiveParallelism)
        {
            Transport.DegreeOfReceiveParallelism = degreeOfReceiveParallelism;
            return this;
        }

        /// <summary>
        /// Sets a custom serialization for <see cref="MessageWrapper" />.
        /// </summary>
        [ObsoleteEx(
            Message = "Configure the transport via the AzureStorageQueueTransport MessageWrapperSerializationDefinition property",
            TreatAsErrorFromVersion = "12.0",
            RemoveInVersion = "13.0")]
        public TransportSettings<AzureStorageQueueTransport> SerializeMessageWrapperWith<TSerializationDefinition>()
            where TSerializationDefinition : SerializationDefinition, new()
        {
            Transport.MessageWrapperSerializationDefinition = new TSerializationDefinition();
            return this;
        }

        /// <summary>
        /// Registers a custom unwrapper to convert native messages to <see cref="MessageWrapper" />. This is needed when receiving raw json/xml/etc messages from non NServiceBus endpoints.
        /// </summary>
        [ObsoleteEx(
            Message = "Configure the transport via the AzureStorageQueueTransport MessageUnwrapper property",
            TreatAsErrorFromVersion = "12.0",
            RemoveInVersion = "13.0")]
        public TransportSettings<AzureStorageQueueTransport> UnwrapMessagesWith(Func<QueueMessage, MessageWrapper> unwrapper)
        {
            Transport.MessageUnwrapper = unwrapper;
            return this;
        }

        /// <summary>
        /// Sets <see cref="QueueServiceClient"/> to be used for messaging operations.
        /// </summary>
        [ObsoleteEx(
            Message = "Configure the transport via the AzureStorageQueueTransport instance constructor",
            TreatAsErrorFromVersion = "11.0",
            RemoveInVersion = "12.0")]
        public TransportSettings<AzureStorageQueueTransport> UseQueueServiceClient(QueueServiceClient queueServiceClient)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sets <see cref="QueueServiceClient"/> to be used for delayed delivery feature.
        /// </summary>
        [ObsoleteEx(
            Message = "Configure the transport via the AzureStorageQueueTransport instance constructor",
            TreatAsErrorFromVersion = "11.0",
            RemoveInVersion = "12.0")]
        public TransportSettings<AzureStorageQueueTransport> UseBlobServiceClient(BlobServiceClient blobServiceClient)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sets <see cref="CloudTableClient"/> to be used for delayed delivery feature.
        /// </summary>
        [ObsoleteEx(
            Message = "Configure the transport via the AzureStorageQueueTransport instance constructor",
            TreatAsErrorFromVersion = "11.0",
            RemoveInVersion = "12.0")]
        public TransportSettings<AzureStorageQueueTransport> UseCloudTableClient(CloudTableClient cloudTableClient)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sets the flag to disable or enable subscriptions caching.
        /// </summary>
        [ObsoleteEx(
            Message = "Configure the transport via the AzureStorageQueueTransport Subscription property",
            TreatAsErrorFromVersion = "12.0",
            RemoveInVersion = "13.0")]
        public static TransportExtensions<AzureStorageQueueTransport> DisableCaching()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Cache subscriptions for a given <see cref="TimeSpan" />.
        /// </summary>
        [ObsoleteEx(
            Message = "Configure the transport via the AzureStorageQueueTransport Subscription property",
            TreatAsErrorFromVersion = "12.0",
            RemoveInVersion = "13.0")]
        public static TransportExtensions<AzureStorageQueueTransport> CacheInvalidationPeriod(TimeSpan cacheInvalidationPeriod)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sets the connection string to be use to connect to the Azure Storage Queue service.
        /// </summary>
        [ObsoleteEx(
            Message = "Configure the transport connection string via the AzureStorageQueueTransport instance constructor",
            TreatAsErrorFromVersion = "11.0",
            RemoveInVersion = "12.0")]
        public TransportSettings<AzureStorageQueueTransport> ConnectionString(string connectionString)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Configures delayed delivery features of this transport.
        /// </summary>
        [ObsoleteEx(
            Message = "Configure the transport via the AzureStorageQueueTransport DelayedDelivery property",
            TreatAsErrorFromVersion = "12.0",
            RemoveInVersion = "13.0")]
        public DelayedDeliverySettings DelayedDelivery()
        {
            return new DelayedDeliverySettings(Transport.DelayedDelivery);
        }

        /// <summary>
        /// Provides access to configure cross account routing.
        /// </summary>
        [ObsoleteEx(
            Message = "Configure the transport via the AzureStorageQueueTransport AccountRouting property",
            TreatAsErrorFromVersion = "12.0",
            RemoveInVersion = "13.0")]
        public AccountRoutingSettings AccountRouting()
        {
            return Transport.AccountRouting;
        }

        /// <summary>
        /// Set default account alias.
        /// </summary>
        [ObsoleteEx(
            Message = "Configure the transport via the AzureStorageQueueTransport AccountRouting.DefaultAccountAlias property",
            TreatAsErrorFromVersion = "12.0",
            RemoveInVersion = "13.0")]
        public TransportSettings<AzureStorageQueueTransport> DefaultAccountAlias(string alias)
        {
            Transport.AccountRouting.DefaultAccountAlias = alias;

            return this;
        }
    }

    /// <summary>Configures native delayed delivery.</summary>
    public partial class DelayedDeliverySettings : ExposeSettings
    {
        NativeDelayedDeliverySettings transportDelayedDelivery;

        internal DelayedDeliverySettings(NativeDelayedDeliverySettings transportDelayedDelivery) : base(new SettingsHolder())
        {
            this.transportDelayedDelivery = transportDelayedDelivery;
        }

        /// <summary>Override the default table name used for storing delayed messages.</summary>
        /// <param name="delayedMessagesTableName">New table name.</param>
        [ObsoleteEx(
            Message = "Configure the transport via the AzureStorageQueueTransport DelayedDelivery.DelayedDeliveryTableName property",
            TreatAsErrorFromVersion = "12.0",
            RemoveInVersion = "13.0")]
        public void UseTableName(string delayedMessagesTableName)
        {
            transportDelayedDelivery.DelayedDeliveryTableName = delayedMessagesTableName;
        }

        /// <summary>
        /// Disable delayed delivery.
        /// <remarks>
        /// Disabling delayed delivery reduces costs associated with polling Azure Storage service for delayed messages that need
        /// to be dispatched.
        /// Do not use this setting if your endpoint requires delayed messages, timeouts, or delayed retries.
        /// </remarks>
        /// </summary>
        [ObsoleteEx(
            Message = "Configure delayed delivery support via the AzureStorageQueueTransport constructor.",
            TreatAsErrorFromVersion = "11.0",
            RemoveInVersion = "12.0")]
        public void DisableDelayedDelivery()
        {
            throw new NotImplementedException();
        }
    }
}

#pragma warning restore CS0618 // Type or member is obsolete
