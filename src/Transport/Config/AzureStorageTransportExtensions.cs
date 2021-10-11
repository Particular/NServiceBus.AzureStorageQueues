namespace NServiceBus
{
    using System;
    using Azure.Transports.WindowsAzureStorageQueues;
    using global::Azure.Storage.Blobs;
    using global::Azure.Storage.Queues;
    using global::Azure.Storage.Queues.Models;
    using Microsoft.Azure.Cosmos.Table;
    using Serialization;

    /// <summary>
    /// Adds extensions methods to <see cref="TransportExtensions{T}" /> for configuration purposes.
    /// </summary>
    public static class AzureStorageTransportExtensions
    {
        /// <summary>
        /// Configures NServiceBus to use the given transport.
        /// </summary>it
        [PreObsolete(
            TreatAsErrorFromVersion = "12.0",
            RemoveInVersion = "13.0",
            ReplacementTypeOrMember = "EndpointConfiguration.UseTransport(TransportDefinition)")]
        public static TransportExtensions<AzureStorageQueueTransport> UseTransport<T>(this EndpointConfiguration config)
            where T : AzureStorageQueueTransport
        {
            var transport = new AzureStorageQueueTransport();
            var routing = config.UseTransport(transport);
            var settings = new TransportExtensions<AzureStorageQueueTransport>(transport, routing);

            return settings;
        }

        /// <summary>
        /// Configures NServiceBus to use the given transport and disable native delayed deliveries
        /// </summary>
        [PreObsolete(
            TreatAsErrorFromVersion = "12.0",
            RemoveInVersion = "13.0",
            ReplacementTypeOrMember = "EndpointConfiguration.UseTransport(TransportDefinition)")]
        public static TransportExtensions<AzureStorageQueueTransport> UseTransport<T>(this EndpointConfiguration config,
            QueueServiceClient queueServiceClient)
            where T : AzureStorageQueueTransport
        {
            var transport = new AzureStorageQueueTransport(queueServiceClient);
            var routing = config.UseTransport(transport);
            var settings = new TransportExtensions<AzureStorageQueueTransport>(transport, routing);

            return settings;
        }

        /// <summary>
        /// Configures NServiceBus to use the given transport with native delayed deliveries support
        /// </summary>
        [PreObsolete(
            TreatAsErrorFromVersion = "12.0",
            RemoveInVersion = "13.0",
            ReplacementTypeOrMember = "EndpointConfiguration.UseTransport(TransportDefinition)")]
        public static TransportExtensions<AzureStorageQueueTransport> UseTransport<T>(this EndpointConfiguration config,
            QueueServiceClient queueServiceClient, BlobServiceClient blobServiceClient,
            CloudTableClient cloudTableClient)
            where T : AzureStorageQueueTransport
        {
            var transport = new AzureStorageQueueTransport(queueServiceClient, blobServiceClient, cloudTableClient);
            var routing = config.UseTransport(transport);
            var settings = new TransportExtensions<AzureStorageQueueTransport>(transport, routing);

            return settings;
        }

        /// <summary>
        /// Controls how long messages should be invisible to other callers when receiving messages from the queue
        /// </summary>
        [PreObsolete(
            Message = "Configure the transport via the AzureStorageQueueTransport MessageInvisibleTime property",
            TreatAsErrorFromVersion = "12.0",
            RemoveInVersion = "13.0")]
        public static TransportExtensions<AzureStorageQueueTransport> MessageInvisibleTime(
            this TransportExtensions<AzureStorageQueueTransport> transport, TimeSpan value)
        {
            transport.Transport.MessageInvisibleTime = value;
            return transport;
        }

        /// <summary>
        /// Sets the amount of time to add to the time to wait before checking for a new message
        /// </summary>
        [PreObsolete(
            Message = "Configure the transport via the AzureStorageQueueTransport PeekInterval property",
            TreatAsErrorFromVersion = "12.0",
            RemoveInVersion = "13.0")]
        public static TransportExtensions<AzureStorageQueueTransport> PeekInterval(
            this TransportExtensions<AzureStorageQueueTransport> transport, TimeSpan value)
        {
            transport.Transport.PeekInterval = value;
            return transport;
        }

        /// <summary>
        /// Sets the maximum amount of time, in milliseconds, that the queue will wait before checking for a new message
        /// </summary>
        [PreObsolete(
            Message = "Configure the transport via the AzureStorageQueueTransport MaximumWaitTimeWhenIdle property",
            TreatAsErrorFromVersion = "12.0",
            RemoveInVersion = "13.0")]
        public static TransportExtensions<AzureStorageQueueTransport> MaximumWaitTimeWhenIdle(
            this TransportExtensions<AzureStorageQueueTransport> transport, TimeSpan value)
        {
            transport.Transport.MaximumWaitTimeWhenIdle = value;
            return transport;
        }

        /// <summary>
        /// Registers a queue name sanitizer to apply to queue names not compliant wth Azure Storage Queue naming rules.
        /// <remarks>By default no sanitization is performed.</remarks>
        /// </summary>
        [PreObsolete(
            Message = "Configure the transport via the AzureStorageQueueTransport QueueNameSanitizer property",
            TreatAsErrorFromVersion = "12.0",
            RemoveInVersion = "13.0")]
        public static TransportExtensions<AzureStorageQueueTransport> SanitizeQueueNamesWith(
            this TransportExtensions<AzureStorageQueueTransport> transport,
            Func<string, string> queueNameSanitizer)
        {
            transport.Transport.QueueNameSanitizer = queueNameSanitizer;
            return transport;
        }

        /// <summary>
        /// Controls how many messages should be read from the queue at once
        /// </summary>
        [PreObsolete(
            Message = "Configure the transport via the AzureStorageQueueTransport ReceiverBatchSize property",
            TreatAsErrorFromVersion = "12.0",
            RemoveInVersion = "13.0")]
        public static TransportExtensions<AzureStorageQueueTransport> BatchSize(
            this TransportExtensions<AzureStorageQueueTransport> transport, int value)
        {
            transport.Transport.ReceiverBatchSize = value;
            return transport;
        }

        /// <summary>
        /// Sets the degree of parallelism that should be used to receive messages.
        /// </summary>
        [PreObsolete(
            Message = "Configure the transport via the AzureStorageQueueTransport DegreeOfReceiveParallelism property",
            TreatAsErrorFromVersion = "12.0",
            RemoveInVersion = "13.0")]
        public static TransportExtensions<AzureStorageQueueTransport> DegreeOfReceiveParallelism(
            this TransportExtensions<AzureStorageQueueTransport> transport, int degreeOfReceiveParallelism)
        {
            transport.Transport.DegreeOfReceiveParallelism = degreeOfReceiveParallelism;
            return transport;
        }

        /// <summary>
        /// Sets a custom serialization for <see cref="MessageWrapper" />.
        /// </summary>
        [PreObsolete(
            Message =
                "Configure the transport via the AzureStorageQueueTransport MessageWrapperSerializationDefinition property",
            TreatAsErrorFromVersion = "12.0",
            RemoveInVersion = "13.0")]
        public static TransportExtensions<AzureStorageQueueTransport>
            SerializeMessageWrapperWith<TSerializationDefinition>(
                this TransportExtensions<AzureStorageQueueTransport> transport)
            where TSerializationDefinition : SerializationDefinition, new()
        {
            transport.Transport.MessageWrapperSerializationDefinition = new TSerializationDefinition();
            return transport;
        }

        /// <summary>
        /// Registers a custom unwrapper to convert native messages to <see cref="MessageWrapper" />. This is needed when receiving raw json/xml/etc messages from non NServiceBus endpoints.
        /// </summary>
        [PreObsolete(
            Message = "Configure the transport via the AzureStorageQueueTransport MessageUnwrapper property",
            TreatAsErrorFromVersion = "12.0",
            RemoveInVersion = "13.0")]
        public static TransportExtensions<AzureStorageQueueTransport> UnwrapMessagesWith(
            this TransportExtensions<AzureStorageQueueTransport> transport,
            Func<QueueMessage, MessageWrapper> unwrapper)
        {
            transport.Transport.MessageUnwrapper = unwrapper;
            return transport;
        }

        /// <summary>
        /// Sets <see cref="QueueServiceClient"/> to be used for messaging operations.
        /// </summary>
        [ObsoleteEx(
            Message =
                "Provide the QueueServiceClient with the UseTransport<AzureStorageQueues> configuration as a parameter.",
            TreatAsErrorFromVersion = "11.0",
            RemoveInVersion = "12.0")]
        public static TransportExtensions<AzureStorageQueueTransport> UseQueueServiceClient(
            this TransportExtensions<AzureStorageQueueTransport> transport,
            QueueServiceClient queueServiceClient)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sets <see cref="QueueServiceClient"/> to be used for delayed delivery feature.
        /// </summary>
        [ObsoleteEx(
            Message =
                "Provide the BlobServiceClient with the UseTransport<AzureStorageQueues> configuration as a parameter.",
            TreatAsErrorFromVersion = "11.0",
            RemoveInVersion = "12.0")]
        public static TransportExtensions<AzureStorageQueueTransport> UseBlobServiceClient(
            this TransportExtensions<AzureStorageQueueTransport> transport, BlobServiceClient blobServiceClient)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sets <see cref="CloudTableClient"/> to be used for delayed delivery feature.
        /// </summary>
        [ObsoleteEx(
            Message =
                "Provide the CloudTableClient with the UseTransport<AzureStorageQueues> configuration as a parameter.",
            TreatAsErrorFromVersion = "11.0",
            RemoveInVersion = "12.0")]
        public static TransportExtensions<AzureStorageQueueTransport> UseCloudTableClient(
            this TransportExtensions<AzureStorageQueueTransport> transport, CloudTableClient cloudTableClient)
        {
            throw new Exception();
        }

        /// <summary>
        /// Sets the flag to disable or enable subscriptions caching.
        /// </summary>
        [PreObsolete(
            Message = "Configure the transport via the AzureStorageQueueTransport Subscription property",
            TreatAsErrorFromVersion = "12.0",
            RemoveInVersion = "13.0")]
        public static TransportExtensions<AzureStorageQueueTransport> DisableCaching(
            this TransportExtensions<AzureStorageQueueTransport> transport)
        {
            transport.Transport.Subscriptions.DisableCaching = true;

            return transport;
        }

        /// <summary>
        /// Cache subscriptions for a given <see cref="TimeSpan" />.
        /// </summary>
        [PreObsolete(
            Message = "Configure the transport via the AzureStorageQueueTransport Subscription property",
            TreatAsErrorFromVersion = "12.0",
            RemoveInVersion = "13.0")]
        public static TransportExtensions<AzureStorageQueueTransport> CacheInvalidationPeriod(
            this TransportExtensions<AzureStorageQueueTransport> transport,
            TimeSpan cacheInvalidationPeriod)
        {
            transport.Transport.Subscriptions.CacheInvalidationPeriod = cacheInvalidationPeriod;

            return transport;
        }

        /// <summary>
        /// Sets the connection string to be use to connect to the Azure Storage Queue service.
        /// </summary>
        [PreObsolete(
            Message =
                "Configure the transport connection string via the AzureStorageQueueTransport instance constructor",
            TreatAsErrorFromVersion = "12.0",
            RemoveInVersion = "13.0")]
        public static TransportExtensions<AzureStorageQueueTransport> ConnectionString(
            this TransportExtensions<AzureStorageQueueTransport> transport, string connectionString)
        {
            transport.Transport.LegacyAPIShimSetConnectionString(connectionString);

            return transport;
        }

        /// <summary>
        /// Configures delayed delivery features of this transport.
        /// </summary>
        [PreObsolete(
            Message = "Configure the transport via the AzureStorageQueueTransport DelayedDelivery property",
            TreatAsErrorFromVersion = "12.0",
            RemoveInVersion = "13.0")]
        public static DelayedDeliverySettings DelayedDelivery(
            this TransportExtensions<AzureStorageQueueTransport> transport) =>
            new DelayedDeliverySettings(transport.Transport.DelayedDelivery);

        /// <summary>
        /// Provides access to configure cross account routing.
        /// </summary>
        [PreObsolete(
            Message = "Configure the transport via the AzureStorageQueueTransport AccountRouting property",
            TreatAsErrorFromVersion = "12.0",
            RemoveInVersion = "13.0")]
        public static AccountRoutingSettings AccountRouting(
            this TransportExtensions<AzureStorageQueueTransport> transport) => transport.Transport.AccountRouting;

        /// <summary>
        /// Set default account alias.
        /// </summary>
        [PreObsolete(
            Message =
                "Configure the transport via the AzureStorageQueueTransport AccountRouting.DefaultAccountAlias property",
            TreatAsErrorFromVersion = "12.0",
            RemoveInVersion = "13.0")]
        public static TransportExtensions<AzureStorageQueueTransport> DefaultAccountAlias(
            this TransportExtensions<AzureStorageQueueTransport> transport, string alias)
        {
            transport.Transport.AccountRouting.DefaultAccountAlias = alias;

            return transport;
        }

        /// <summary>
        /// Override the default table name used for storing subscriptions.
        /// </summary>
        /// <remarks>All endpoints in a given account need to agree on that name in order for them to be able to subscribe to and publish events.</remarks>
        [PreObsolete(
            Message = "Configure the transport via the AzureStorageQueueTransport Subscription property",
            TreatAsErrorFromVersion = "12.0",
            RemoveInVersion = "13.0")]
        public static TransportExtensions<AzureStorageQueueTransport> SubscriptionTableName(
            this TransportExtensions<AzureStorageQueueTransport> transport, string subscriptionTableName)
        {
            transport.Transport.Subscriptions.SubscriptionTableName = subscriptionTableName;

            return transport;
        }
    }
}