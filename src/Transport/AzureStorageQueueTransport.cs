using System.Text;
using Azure.Storage.Blobs;
using Microsoft.Azure.Cosmos.Table;

namespace NServiceBus
{
    using global::Azure.Storage.Queues;
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using MessageInterfaces;
    using Serialization;
    using Settings;
    using Transport;
    using Transport.AzureStorageQueues;

    /// <summary>
    /// Transport definition for AzureStorageQueue
    /// </summary>
    public class AzureStorageQueueTransport : TransportDefinition
    {
        internal const string SerializerSettingsKey = "MainSerializer";

        internal static IMessageSerializer GetMainSerializer(IMessageMapper mapper, ReadOnlySettings settings)
        {
            var definitionAndSettings = settings.Get<Tuple<SerializationDefinition, SettingsHolder>>(SerializerSettingsKey);
            var definition = definitionAndSettings.Item1;
            var serializerSettings = definitionAndSettings.Item2;

            // serializerSettings.Merge(settings);
            var merge = typeof(SettingsHolder).GetMethod("Merge", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            merge.Invoke(serializerSettings, new object[]
            {
                settings
            });

            var serializerFactory = definition.Configure(serializerSettings);
            var serializer = serializerFactory(mapper);
            return serializer;
        }

        /// <summary>
        /// Initialize a new transport definition for AzureStorageQueue
        /// </summary>
        public AzureStorageQueueTransport(string connectionString, bool disableNativeDelayedDeliveries = false) : base(TransportTransactionMode.ReceiveOnly)
        {
            Guard.AgainstNullAndEmpty(nameof(connectionString), connectionString);

            queueServiceClientProvider = new ConnectionStringQueueServiceClientProvider(connectionString);
            supportsDelayedDelivery = !disableNativeDelayedDeliveries;
            if (supportsDelayedDelivery)
            {
                blobServiceClientProvider = new ConnectionStringBlobServiceClientProvider(connectionString);
                cloudTableClientProvider = new ConnectionStringCloudTableClientProvider(connectionString);
            }
        }

        /// <summary>
        /// Initialize a new transport definition for AzureStorageQueue and disable native delayed deliveries
        /// </summary>
        public AzureStorageQueueTransport(QueueServiceClient queueServiceClient) : base(TransportTransactionMode.ReceiveOnly)
        {
            Guard.AgainstNull(nameof(queueServiceClient), queueServiceClient);

            queueServiceClientProvider = new UserQueueServiceClientProvider(queueServiceClient);

            supportsDelayedDelivery = false;
        }

        /// <summary>
        /// Initialize a new transport definition for AzureStorageQueue with native delayed deliveries support
        /// </summary>
        public AzureStorageQueueTransport(QueueServiceClient queueServiceClient, BlobServiceClient blobServiceClient, CloudTableClient cloudTableClient)
            : base(TransportTransactionMode.ReceiveOnly)
        {
            Guard.AgainstNull(nameof(queueServiceClient), queueServiceClient);
            Guard.AgainstNull(nameof(blobServiceClient), blobServiceClient);
            Guard.AgainstNull(nameof(cloudTableClient), cloudTableClient);

            queueServiceClientProvider = new UserQueueServiceClientProvider(queueServiceClient);
            blobServiceClientProvider = new UserBlobServiceClientProvider(blobServiceClient);
            cloudTableClientProvider = new UserCloudTableClientProvider(cloudTableClient);

            supportsDelayedDelivery = true;
        }

        /// <inheritdoc cref="Initialize"/>
        public override Task<TransportInfrastructure> Initialize(HostSettings hostSettings, ReceiveSettings[] receivers, string[] sendingAddresses,
            CancellationToken cancellationToken = new CancellationToken())
        {
            Guard.AgainstNull(nameof(hostSettings), hostSettings);
            Guard.AgainstNull(nameof(receivers), receivers);
            Guard.AgainstNull(nameof(sendingAddresses), sendingAddresses);

            queueAddressGenerator = new QueueAddressGenerator(QueueNameSanitizer);

            //TODO: investigate if this is really needed
            //Guard.AgainstUnsetSerializerSetting(settings);

            var infrastructure = new AzureStorageQueueInfrastructure(
                MessageInvisibleTime,
                PeekInterval,
                MaximumWaitTimeWhenIdle,
                SupportsDelayedDelivery,
                queueAddressGenerator,
                queueServiceClientProvider,
                blobServiceClientProvider,
                cloudTableClientProvider);

            return Task.FromResult<TransportInfrastructure>(infrastructure);
        }

        /// <inheritdoc cref="ToTransportAddress"/>
        public override string ToTransportAddress(Transport.QueueAddress address)
        {
            var queue = new StringBuilder(address.BaseAddress);

            if (address.Discriminator != null)
            {
                queue.Append("-" + address.Discriminator);
            }

            if (address.Qualifier != null)
            {
                queue.Append("-" + address.Qualifier);
            }

            return queueAddressGenerator.GetQueueName(queue.ToString());
        }

        /// <inheritdoc cref="GetSupportedTransactionModes"/>
        public override IReadOnlyCollection<TransportTransactionMode> GetSupportedTransactionModes()
        {
            return supportedTransactionModes;
        }

        /// <inheritdoc cref="SupportsDelayedDelivery"/>
        public override bool SupportsDelayedDelivery => supportsDelayedDelivery;

        /// <inheritdoc cref="SupportsPublishSubscribe"/>
        public override bool SupportsPublishSubscribe { get; } = false;

        /// <inheritdoc cref="SupportsTTBR"/>
        public override bool SupportsTTBR { get; } = false;

        /// <summary>
        /// Controls how long messages should be invisible to other callers when receiving messages from the queue
        /// </summary>
        public TimeSpan MessageInvisibleTime
        {
            get => messageInvisibleTime;
            set
            {
                if (value < TimeSpan.FromSeconds(1) || value > TimeSpan.FromDays(7))
                {
                    throw new ArgumentOutOfRangeException(nameof(MessageInvisibleTime), value, "Value must be between 1 second and 7 days.");
                }
                messageInvisibleTime = value;
            }
        }

        /// <summary>
        /// The amount of time to add to the time to wait before checking for a new message
        /// </summary>
        public TimeSpan PeekInterval
        {
            get => peekInterval;
            set
            {
                Guard.AgainstNegativeAndZero(nameof(PeekInterval), value);
                peekInterval = value;
            }
        }

        /// <summary>
        /// The maximum amount of time, in milliseconds, that the transport will wait before checking for a new message
        /// </summary>
        public TimeSpan MaximumWaitTimeWhenIdle
        {
            get => maximumWaitTimeWhenIdle;
            set
            {
                if (value < TimeSpan.FromMilliseconds(100) || value > TimeSpan.FromSeconds(60))
                {
                    throw new ArgumentOutOfRangeException(nameof(MaximumWaitTimeWhenIdle), value, "Value must be between 100ms and 60 seconds.");
                }

                maximumWaitTimeWhenIdle = value;
            }
        }

        /// <summary>
        /// Defines a queue name sanitizer to apply to queue names not compliant wth Azure Storage Queue naming rules.
        /// <remarks>By default no sanitization is performed.</remarks>
        /// </summary>
        public Func<string, string> QueueNameSanitizer
        {
            get => queueNameSanitizer;
            set
            {
                Guard.AgainstNull(nameof(QueueNameSanitizer), value);

                Func<string, string> queueNameSanitizerWrapper = entityName =>
                {
                    try
                    {
                        return value(entityName);
                    }
                    catch (Exception exception)
                    {
                        throw new Exception("Registered queue name sanitizer threw an exception.", exception);
                    }
                };

                queueNameSanitizer = queueNameSanitizerWrapper;
            }
        }

        private readonly TransportTransactionMode[] supportedTransactionModes = new[] {TransportTransactionMode.None, TransportTransactionMode.ReceiveOnly};
        private TimeSpan messageInvisibleTime = DefaultConfigurationValues.DefaultMessageInvisibleTime;
        private TimeSpan peekInterval = DefaultConfigurationValues.DefaultPeekInterval;
        private TimeSpan maximumWaitTimeWhenIdle = DefaultConfigurationValues.DefaultMaximumWaitTimeWhenIdle;
        private Func<string, string> queueNameSanitizer = DefaultConfigurationValues.DefaultQueueNameSanitizer;
        private QueueAddressGenerator queueAddressGenerator;
        private IQueueServiceClientProvider queueServiceClientProvider;
        private readonly bool supportsDelayedDelivery = true;
        private IBlobServiceClientProvider blobServiceClientProvider;
        private ICloudTableClientProvider cloudTableClientProvider;
    }
}