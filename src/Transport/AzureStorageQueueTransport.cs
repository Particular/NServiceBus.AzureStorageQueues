using System.Text.RegularExpressions;

namespace NServiceBus
{
    using global::Azure.Storage.Queues;
    using global::Azure.Storage.Blobs;
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Table;
    using Serialization;
    using Transport;
    using Transport.AzureStorageQueues;
    using Azure.Transports.WindowsAzureStorageQueues;

    /// <summary>
    /// Transport definition for AzureStorageQueue
    /// </summary>
    public class AzureStorageQueueTransport : TransportDefinition
    {
        /// <summary>
        /// Initialize a new transport definition for AzureStorageQueue
        /// </summary>
        public AzureStorageQueueTransport(string connectionString, bool disableNativeDelayedDeliveries = false)
            : base(TransportTransactionMode.ReceiveOnly, !disableNativeDelayedDeliveries, false, true)
        {
            Guard.AgainstNullAndEmpty(nameof(connectionString), connectionString);

            queueServiceClientProvider = new ConnectionStringQueueServiceClientProvider(connectionString);
            if (SupportsDelayedDelivery)
            {
                blobServiceClientProvider = new ConnectionStringBlobServiceClientProvider(connectionString);
                cloudTableClientProvider = new ConnectionStringCloudTableClientProvider(connectionString);
            }
        }

        /// <summary>
        /// Initialize a new transport definition for AzureStorageQueue and disable native delayed deliveries
        /// </summary>
        public AzureStorageQueueTransport(QueueServiceClient queueServiceClient)
            : base(TransportTransactionMode.ReceiveOnly, false, false, true)
        {
            Guard.AgainstNull(nameof(queueServiceClient), queueServiceClient);

            queueServiceClientProvider = new UserQueueServiceClientProvider(queueServiceClient);
        }

        /// <summary>
        /// Initialize a new transport definition for AzureStorageQueue with native delayed deliveries support
        /// </summary>
        public AzureStorageQueueTransport(QueueServiceClient queueServiceClient, BlobServiceClient blobServiceClient, CloudTableClient cloudTableClient)
            : base(TransportTransactionMode.ReceiveOnly, true, false, true)
        {
            Guard.AgainstNull(nameof(queueServiceClient), queueServiceClient);
            Guard.AgainstNull(nameof(blobServiceClient), blobServiceClient);
            Guard.AgainstNull(nameof(cloudTableClient), cloudTableClient);

            queueServiceClientProvider = new UserQueueServiceClientProvider(queueServiceClient);
            blobServiceClientProvider = new UserBlobServiceClientProvider(blobServiceClient);
            cloudTableClientProvider = new UserCloudTableClientProvider(cloudTableClient);
        }

        /// <inheritdoc cref="Initialize"/>
        public override Task<TransportInfrastructure> Initialize(HostSettings hostSettings, ReceiveSettings[] receivers, string[] sendingAddresses,
            CancellationToken cancellationToken = new CancellationToken())
        {
            Guard.AgainstNull(nameof(hostSettings), hostSettings);
            Guard.AgainstNull(nameof(receivers), receivers);
            Guard.AgainstNull(nameof(sendingAddresses), sendingAddresses);

            queueAddressGenerator = new QueueAddressGenerator(QueueNameSanitizer);

            var infrastructure = new AzureStorageQueueInfrastructure(
                hostSettings,
                TransportTransactionMode,
                MessageInvisibleTime,
                PeekInterval,
                MaximumWaitTimeWhenIdle,
                SupportsDelayedDelivery,
                ReceiverBatchSize,
                DegreeOfReceiveParallelism,
                queueAddressGenerator,
                DelayedDeliveryTableName,
                queueServiceClientProvider,
                blobServiceClientProvider,
                cloudTableClientProvider,
                MessageWrapperSerializationDefinition);

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

        /// <summary>
        /// Controls how many messages should be read from the queue at once
        /// </summary>
        public int? ReceiverBatchSize
        {
            get => receiverBatchSize;
            set
            {
                if (value < 1 || value > 32)
                {
                    throw new ArgumentOutOfRangeException(nameof(ReceiverBatchSize), value, "Batchsize must be between 1 and 32 messages.");
                }
                receiverBatchSize = value;
            }
        }

        /// <summary>
        /// Sets the degree of parallelism that should be used to receive messages.
        /// </summary>
        public int? DegreeOfReceiveParallelism
        {
            get => degreeOfReceiveParallelism;
            set
            {
                const int maxDegreeOfReceiveParallelism = 32;

                if (degreeOfReceiveParallelism < 1 || degreeOfReceiveParallelism > maxDegreeOfReceiveParallelism)
                {
                    throw new ArgumentOutOfRangeException(nameof(DegreeOfReceiveParallelism), value, $"DegreeOfParallelism must be between 1 and {maxDegreeOfReceiveParallelism}.");
                }

                degreeOfReceiveParallelism = value;
            }
        }

        /// <summary>
        /// Sets a custom serialization for <see cref="MessageWrapper" />.
        /// </summary>
        public SerializationDefinition MessageWrapperSerializationDefinition
        {
            get => messageWrapperSerializationDefinition;
            set => messageWrapperSerializationDefinition = value;
        }

        /// <summary>
        /// Override the default table name used for storing delayed messages.
        /// </summary>
        public string DelayedDeliveryTableName
        {
            get => delayedDeliveryTableName;
            set
            {
                Guard.AgainstNullAndEmpty(nameof(DelayedDeliveryTableName), value);

                if (delayedDeliveryTableNameRegex.IsMatch(DelayedDeliveryTableName) == false)
                {
                    throw new ArgumentException($"{nameof(DelayedDeliveryTableName)} must match the following regular expression '{delayedDeliveryTableNameRegex}'");
                }

                delayedDeliveryTableName = value;
            }
        }

        private readonly TransportTransactionMode[] supportedTransactionModes = new[] {TransportTransactionMode.None, TransportTransactionMode.ReceiveOnly};
        private TimeSpan messageInvisibleTime = DefaultConfigurationValues.DefaultMessageInvisibleTime;
        private TimeSpan peekInterval = DefaultConfigurationValues.DefaultPeekInterval;
        private TimeSpan maximumWaitTimeWhenIdle = DefaultConfigurationValues.DefaultMaximumWaitTimeWhenIdle;
        private Func<string, string> queueNameSanitizer = DefaultConfigurationValues.DefaultQueueNameSanitizer;
        private QueueAddressGenerator queueAddressGenerator;
        private IQueueServiceClientProvider queueServiceClientProvider;
        private IBlobServiceClientProvider blobServiceClientProvider;
        private ICloudTableClientProvider cloudTableClientProvider;
        private int? receiverBatchSize = DefaultConfigurationValues.DefaultBatchSize;
        private int? degreeOfReceiveParallelism;
        private SerializationDefinition messageWrapperSerializationDefinition;
        private string delayedDeliveryTableName;
        static readonly Regex delayedDeliveryTableNameRegex = new Regex(@"^[A-Za-z][A-Za-z0-9]{2,62}$", RegexOptions.Compiled);
    }
}