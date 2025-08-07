namespace NServiceBus
{
    using System;
    using Azure.Transports.WindowsAzureStorageQueues;
    using global::Azure.Data.Tables;
    using global::Azure.Storage.Blobs;
    using global::Azure.Storage.Queues;
    using global::Azure.Storage.Queues.Models;
    using Particular.Obsoletes;
    using Serialization;

    /// <summary>
    /// Adds extensions methods to <see cref="TransportExtensions{T}" /> for configuration purposes.
    /// </summary>
    public static partial class AzureStorageTransportExtensions
    {
        /// <summary>
        /// Configures NServiceBus to use the given transport.
        /// </summary>it
        [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
            ReplacementTypeOrMember = "EndpointConfiguration.UseTransport(TransportDefinition)",
            Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.")]
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
        [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
            ReplacementTypeOrMember = "EndpointConfiguration.UseTransport(TransportDefinition)",
            Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.")]
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
        [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
            ReplacementTypeOrMember = "EndpointConfiguration.UseTransport(TransportDefinition)",
            Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.")]
        public static TransportExtensions<AzureStorageQueueTransport> UseTransport<T>(this EndpointConfiguration config,
            QueueServiceClient queueServiceClient, BlobServiceClient blobServiceClient,
            TableServiceClient tableServiceClient)
            where T : AzureStorageQueueTransport
        {
            var transport = new AzureStorageQueueTransport(queueServiceClient, blobServiceClient, tableServiceClient);
            var routing = config.UseTransport(transport);
            var settings = new TransportExtensions<AzureStorageQueueTransport>(transport, routing);

            return settings;
        }

        /// <summary>
        /// Controls how long messages should be invisible to other callers when receiving messages from the queue
        /// </summary>
        [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
            ReplacementTypeOrMember = "AzureStorageQueueTransport.MessageInvisibleTime",
            Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.")]
        public static TransportExtensions<AzureStorageQueueTransport> MessageInvisibleTime(
            this TransportExtensions<AzureStorageQueueTransport> config, TimeSpan value)
        {
            config.Transport.MessageInvisibleTime = value;
            return config;
        }

        /// <summary>
        /// Sets the amount of time to add to the time to wait before checking for a new message
        /// </summary>
        [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
            ReplacementTypeOrMember = "AzureStorageQueueTransport.PeekInterval",
            Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.")]
        public static TransportExtensions<AzureStorageQueueTransport> PeekInterval(
            this TransportExtensions<AzureStorageQueueTransport> config, TimeSpan value)
        {
            config.Transport.PeekInterval = value;
            return config;
        }

        /// <summary>
        /// Sets the maximum amount of time, in milliseconds, that the queue will wait before checking for a new message
        /// </summary>
        [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
            ReplacementTypeOrMember = "AzureStorageQueueTransport.MaximumWaitTimeWhenIdle",
            Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.")]
        public static TransportExtensions<AzureStorageQueueTransport> MaximumWaitTimeWhenIdle(
            this TransportExtensions<AzureStorageQueueTransport> config, TimeSpan value)
        {
            config.Transport.MaximumWaitTimeWhenIdle = value;
            return config;
        }

        /// <summary>
        /// Registers a queue name sanitizer to apply to queue names not compliant wth Azure Storage Queue naming rules.
        /// <remarks>By default no sanitization is performed.</remarks>
        /// </summary>
        [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
            ReplacementTypeOrMember = "AzureStorageQueueTransport.QueueNameSanitizer",
            Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.")]
        public static TransportExtensions<AzureStorageQueueTransport> SanitizeQueueNamesWith(
            this TransportExtensions<AzureStorageQueueTransport> config,
            Func<string, string> queueNameSanitizer)
        {
            config.Transport.QueueNameSanitizer = queueNameSanitizer;
            return config;
        }

        /// <summary>
        /// Controls how many messages should be read from the queue at once
        /// </summary>
        [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
            ReplacementTypeOrMember = "AzureStorageQueueTransport.ReceiverBatchSize",
            Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.")]
        public static TransportExtensions<AzureStorageQueueTransport> BatchSize(
            this TransportExtensions<AzureStorageQueueTransport> config, int value)
        {
            config.Transport.ReceiverBatchSize = value;
            return config;
        }

        /// <summary>
        /// Sets the degree of parallelism that should be used to receive messages.
        /// </summary>
        [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
            ReplacementTypeOrMember = "AzureStorageQueueTransport.DegreeOfReceiveParallelism",
            Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.")]
        public static TransportExtensions<AzureStorageQueueTransport> DegreeOfReceiveParallelism(
            this TransportExtensions<AzureStorageQueueTransport> config, int degreeOfReceiveParallelism)
        {
            config.Transport.DegreeOfReceiveParallelism = degreeOfReceiveParallelism;
            return config;
        }

        /// <summary>
        /// Sets a custom serialization for <see cref="MessageWrapper" />.
        /// </summary>
        [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
            ReplacementTypeOrMember = "AzureStorageQueueTransport.MessageWrapperSerializationDefinition",
            Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.")]
        public static TransportExtensions<AzureStorageQueueTransport>
            SerializeMessageWrapperWith<TSerializationDefinition>(
                this TransportExtensions<AzureStorageQueueTransport> config)
            where TSerializationDefinition : SerializationDefinition, new()
        {
            config.Transport.MessageWrapperSerializationDefinition = new TSerializationDefinition();
            return config;
        }

        /// <summary>
        /// Registers a custom unwrapper to convert native messages to <see cref="MessageWrapper" />. This is needed when receiving raw json/xml/etc messages from non NServiceBus endpoints.
        /// </summary>
        [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
            ReplacementTypeOrMember = "AzureStorageQueueTransport.MessageUnwrapper",
            Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.")]
        public static TransportExtensions<AzureStorageQueueTransport> UnwrapMessagesWith(
            this TransportExtensions<AzureStorageQueueTransport> config,
            Func<QueueMessage, MessageWrapper> unwrapper)
        {
            config.Transport.MessageUnwrapper = unwrapper;
            return config;
        }

        /// <summary>
        /// Sets the flag to disable or enable subscriptions caching.
        /// </summary>
        [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
            ReplacementTypeOrMember = "AzureStorageQueueTransport.Subscriptions.DisableCaching",
            Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.")]
        public static TransportExtensions<AzureStorageQueueTransport> DisableCaching(
            this TransportExtensions<AzureStorageQueueTransport> config)
        {
            config.Transport.Subscriptions.DisableCaching = true;

            return config;
        }

        /// <summary>
        /// Cache subscriptions for a given <see cref="TimeSpan" />.
        /// </summary>
        [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
            ReplacementTypeOrMember = "AzureStorageQueueTransport Subscriptions.CacheInvalidationPeriod",
            Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.")]
        public static TransportExtensions<AzureStorageQueueTransport> CacheInvalidationPeriod(
            this TransportExtensions<AzureStorageQueueTransport> config,
            TimeSpan cacheInvalidationPeriod)
        {
            config.Transport.Subscriptions.CacheInvalidationPeriod = cacheInvalidationPeriod;

            return config;
        }

        /// <summary>
        /// Sets the connection string to be use to connect to the Azure Storage Queue service.
        /// </summary>
        [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
            Message = "Configure the transport connection string via the AzureStorageQueueTransport instance constructor",
            Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.")]
        public static TransportExtensions<AzureStorageQueueTransport> ConnectionString(
            this TransportExtensions<AzureStorageQueueTransport> config, string connectionString)
        {
            config.Transport.LegacyAPIShimSetConnectionString(connectionString);

            return config;
        }

        /// <summary>
        /// Sets the connection string to be use to connect to the Azure Storage Queue service.
        /// </summary>
        [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
            Message = "Configure the transport connection string via the AzureStorageQueueTransport instance constructor",
            Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.")]
        public static TransportExtensions<AzureStorageQueueTransport> ConnectionString(
            this TransportExtensions<AzureStorageQueueTransport> config, Func<string> connectionString)
        {
            config.Transport.LegacyAPIShimSetConnectionString(connectionString());

            return config;
        }

        /// <summary>
        /// Configures delayed delivery features of this transport.
        /// </summary>
        [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
            ReplacementTypeOrMember = "AzureStorageQueueTransport.DelayedDelivery",
            Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.")]
        public static DelayedDeliverySettings DelayedDelivery(
            this TransportExtensions<AzureStorageQueueTransport> config) =>
            new(config.Transport.DelayedDelivery);

        /// <summary>
        /// Provides access to configure cross account routing.
        /// </summary>
        [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
            ReplacementTypeOrMember = "AzureStorageQueueTransport.AccountRouting",
            Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.")]
        public static AccountRoutingSettings AccountRouting(
            this TransportExtensions<AzureStorageQueueTransport> config) => config.Transport.AccountRouting;

        /// <summary>
        /// Set default account alias.
        /// </summary>
        [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
            ReplacementTypeOrMember = "AzureStorageQueueTransport.AccountRouting.DefaultAccountAlias",
            Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.")]
        public static TransportExtensions<AzureStorageQueueTransport> DefaultAccountAlias(
            this TransportExtensions<AzureStorageQueueTransport> config, string alias)
        {
            config.Transport.AccountRouting.DefaultAccountAlias = alias;

            return config;
        }

        /// <summary>
        /// Override the default table name used for storing subscriptions.
        /// </summary>
        /// <remarks>All endpoints in a given account need to agree on that name in order for them to be able to subscribe to and publish events.</remarks>
        [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
            ReplacementTypeOrMember = "AzureStorageQueueTransport.Subscriptions.SubscriptionTableName",
            Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.")]
        public static TransportExtensions<AzureStorageQueueTransport> SubscriptionTableName(
            this TransportExtensions<AzureStorageQueueTransport> config, string subscriptionTableName)
        {
            config.Transport.Subscriptions.SubscriptionTableName = subscriptionTableName;

            return config;
        }

        /// <summary>
        /// Enables compatibility with endpoints running on message-driven pub-sub
        /// </summary>
        /// <param name="transportExtensions">The transport to enable pub-sub compatibility on</param>
        [PreObsolete("https://github.com/Particular/NServiceBus/issues/6471",
           Message = "Native publish/subscribe is always enabled in version 11. All endpoints must be updated to use native publish/subscribe before updating to this version.",
           Note = "As long as core supports message-driven publish/subscribe migration mode, then the transports must continue to support it too.")]
        public static SubscriptionMigrationModeSettings EnableMessageDrivenPubSubCompatibilityMode(this TransportExtensions<AzureStorageQueueTransport> transportExtensions)
        {
            var subscriptionMigrationModeSettings = transportExtensions.Routing().EnableMessageDrivenPubSubCompatibilityMode();

            return subscriptionMigrationModeSettings;
        }
    }
}