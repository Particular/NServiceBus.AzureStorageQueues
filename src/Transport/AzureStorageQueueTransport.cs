namespace NServiceBus
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading.Tasks;
    using Azure.Transports.WindowsAzureStorageQueues;
    using global::Azure.Storage.Blobs;
    using global::Azure.Storage.Queues;
    using global::Azure.Storage.Queues.Models;
    using Microsoft.Azure.Cosmos.Table;
    using MessageInterfaces;
    using Routing;
    using Settings;
    using Serialization;
    using Transport;
    using Transport.AzureStorageQueues;
    using System.Globalization;

    /// <summary>
    /// Transport definition for AzureStorageQueue
    /// </summary>
    public class AzureStorageQueueTransport : TransportDefinition, IMessageDrivenSubscriptionTransport
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

        static string GenerateDelayedDeliveryTableName(string endpointName)
        {
            byte[] hashedName;
            using (var sha1 = new SHA1Managed())
            {
                sha1.Initialize();
                hashedName = sha1.ComputeHash(Encoding.UTF8.GetBytes(endpointName));
            }

            var hashName = BitConverter.ToString(hashedName).Replace("-", string.Empty);
            return "delays" + hashName.ToLower();
        }

        /// <inheritdoc cref="Initialize"/>
        public override async Task<TransportInfrastructure> Initialize(HostSettings hostSettings, ReceiveSettings[] receivers, string[] sendingAddresses)
        {
            Guard.AgainstNull(nameof(hostSettings), hostSettings);
            Guard.AgainstNull(nameof(receivers), receivers);
            Guard.AgainstNull(nameof(sendingAddresses), sendingAddresses);

            ValidateReceiversSettings(receivers);

            if (hostSettings.SetupInfrastructure)
            {
                var queuesToCreate = receivers.Select(settings => settings.ReceiveAddress).Union(sendingAddresses).ToList();
                if (SupportsDelayedDelivery && !string.IsNullOrWhiteSpace(DelayedDelivery.DelayedDeliveryPoisonQueue))
                {
                    queuesToCreate.Add(DelayedDelivery.DelayedDeliveryPoisonQueue);
                }

                var queueCreator = new AzureMessageQueueCreator(queueServiceClientProvider, GetQueueAddressGenerator());
                await queueCreator.CreateQueueIfNecessary(queuesToCreate)
                    .ConfigureAwait(false);
            }

            var azureStorageAddressing = new AzureStorageAddressingSettings(GetQueueAddressGenerator());
            azureStorageAddressing.RegisterMapping(AccountRouting.DefaultAccountAlias ?? "", AccountRouting.mappings);
            azureStorageAddressing.Add(new AccountInfo("", queueServiceClientProvider.Client), false);

            object delayedDeliveryPersistenceDiagnosticSection = new { };
            CloudTable delayedMessagesStorageTable = null;
            var nativeDelayedDeliveryPersistence = NativeDelayDeliveryPersistence.Disabled();
            if (SupportsDelayedDelivery)
            {
                delayedMessagesStorageTable = await EnsureNativeDelayedDeliveryTable(
                    hostSettings.Name,
                    DelayedDelivery.DelayedDeliveryTableName,
                    cloudTableClientProvider.Client,
                    hostSettings.SetupInfrastructure).ConfigureAwait(false);

                nativeDelayedDeliveryPersistence = new NativeDelayDeliveryPersistence(delayedMessagesStorageTable);

                delayedDeliveryPersistenceDiagnosticSection = new
                {
                    NativeDelayedDeliveryTableName = delayedMessagesStorageTable.Name,
                    UserDefinedNativeDelayedDeliveryTableName = !string.IsNullOrWhiteSpace(DelayedDelivery.DelayedDeliveryTableName)
                };
            }

            var serializer = BuildSerializer(MessageWrapperSerializationDefinition, hostSettings.CoreSettings ?? new SettingsHolder());
            var dispatcher = new Dispatcher(GetQueueAddressGenerator(), azureStorageAddressing, serializer, nativeDelayedDeliveryPersistence);

            object delayedDeliveryProcessorDiagnosticSection = new { };
            var nativeDelayedDeliveryProcessor = NativeDelayedDeliveryProcessor.Disabled();
            if (SupportsDelayedDelivery)
            {
                var nativeDelayedDeliveryErrorQueue = DelayedDelivery.DelayedDeliveryPoisonQueue
                    ?? hostSettings.CoreSettings?.GetOrDefault<string>(ErrorQueueSettings.SettingsKey)
                    ?? receivers.Select(settings => settings.ErrorQueue).FirstOrDefault();

                nativeDelayedDeliveryProcessor = new NativeDelayedDeliveryProcessor(
                        dispatcher,
                        delayedMessagesStorageTable,
                        blobServiceClientProvider.Client,
                        nativeDelayedDeliveryErrorQueue,
                        TransportTransactionMode,
                        new BackoffStrategy(PeekInterval, MaximumWaitTimeWhenIdle));
                nativeDelayedDeliveryProcessor.Start();

                delayedDeliveryProcessorDiagnosticSection = new
                {
                    DelayedDeliveryPoisonQueue = nativeDelayedDeliveryErrorQueue,
                    UserDefinedDelayedDeliveryPoisonQueue = !string.IsNullOrWhiteSpace(DelayedDelivery.DelayedDeliveryPoisonQueue)
                };
            }

            var messageReceivers = receivers.Select(settings => BuildReceiver(settings, serializer, hostSettings.CriticalErrorAction)).ToList();

            var infrastructure = new AzureStorageQueueInfrastructure(
                dispatcher,
                new ReadOnlyCollection<IMessageReceiver>(messageReceivers),
                nativeDelayedDeliveryProcessor);

            hostSettings.StartupDiagnostic.Add("NServiceBus.Transport.AzureStorageQueues", new
            {
                ConnectionMechanism = new
                {
                    Queue = queueServiceClientProvider is ConnectionStringQueueServiceClientProvider ? "ConnectionString" : "QueueServiceClient",
                    Table = cloudTableClientProvider is ConnectionStringCloudTableClientProvider ? "ConnectionString" : "CloudTableClient",
                    Blob = blobServiceClientProvider is ConnectionStringBlobServiceClientProvider ? "ConnectionString" : "BlobServiceClient",
                },
                MessageWrapperSerializer = MessageWrapperSerializationDefinition == null ? "Default" : "Custom",
                MessageEnvelopeUnwrapper = MessageUnwrapper == null ? "Default" : "Custom",
                NativeDelayedDelivery = new
                {
                    IsEnabled = SupportsDelayedDelivery,
                    Processor = nativeDelayedDeliveryProcessor,
                    Persistence = nativeDelayedDeliveryPersistence
                },
                TransactionMode = Enum.GetName(typeof(TransportTransactionMode), TransportTransactionMode),
                ReceiverBatchSize = ReceiverBatchSize.HasValue ? ReceiverBatchSize.Value.ToString(CultureInfo.InvariantCulture) : "Default",
                DegreeOfReceiveParallelism = DegreeOfReceiveParallelism.HasValue ? DegreeOfReceiveParallelism.Value.ToString(CultureInfo.InvariantCulture) : "Default",
                MaximumWaitTimeWhenIdle,
                PeekInterval,
                MessageInvisibleTime
            });

            return infrastructure;
        }

        void ValidateReceiversSettings(ReceiveSettings[] receivers)
        {
            var isSendOnly = receivers.Length == 0;
            if (SupportsDelayedDelivery && isSendOnly && string.IsNullOrWhiteSpace(DelayedDelivery.DelayedDeliveryPoisonQueue))
            {
                throw new Exception($"Send only endpoints require a native delayed poison queue." +
                                    $" Configure a user defined poison queue for delayed deliveries by using the" +
                                    $" {nameof(AzureStorageQueueTransport)}.{nameof(DelayedDelivery)}" +
                                    $".{nameof(DelayedDelivery.DelayedDeliveryPoisonQueue)} property.");
            }
        }

        static async Task<CloudTable> EnsureNativeDelayedDeliveryTable(string endpointName, string delayedDeliveryTableName, CloudTableClient cloudTableClient, bool setupInfrastructure)
        {
            if (string.IsNullOrEmpty(delayedDeliveryTableName))
            {
                delayedDeliveryTableName = GenerateDelayedDeliveryTableName(endpointName);
            }

            var delayedMessagesStorageTable = cloudTableClient.GetTableReference(delayedDeliveryTableName);
            if (setupInfrastructure)
            {
                await delayedMessagesStorageTable.CreateIfNotExistsAsync().ConfigureAwait(false);
            }

            return delayedMessagesStorageTable;
        }

        static MessageWrapperSerializer BuildSerializer(SerializationDefinition userWrapperSerializationDefinition, ReadOnlySettings settings)
        {
            return userWrapperSerializationDefinition != null
                ? new MessageWrapperSerializer(userWrapperSerializationDefinition.Configure(settings).Invoke(MessageWrapperSerializer.GetMapper()))
                : new MessageWrapperSerializer(GetMainSerializerHack(MessageWrapperSerializer.GetMapper(), settings));
        }

        internal static IMessageSerializer GetMainSerializerHack(IMessageMapper mapper, ReadOnlySettings settings)
        {
            if (!settings.TryGet<Tuple<SerializationDefinition, SettingsHolder>>(SerializerSettingsKey, out var serializerSettingsTuple))
            {
                throw new Exception("No serializer defined. If the transport is used in combination with NServiceBus, " +
                                    "use 'endpointConfiguration.UseSerialization<T>();' to select a serializer. " +
                                    "If you are upgrading, install the `NServiceBus.Newtonsoft.Json` NuGet package " +
                                    "and consult the upgrade guide for further information. If the transport is used in isolation, " +
                                    "set a serializer definition in an empty SettingsHolder instance and invoke ValidateNServiceBusSettings() " +
                                    "before starting the transport.");
            }

            var (definition, serializerSettings) = serializerSettingsTuple;

            var serializerFactory = definition.Configure(settings);
            var serializer = serializerFactory(mapper);
            return serializer;
        }

        IMessageReceiver BuildReceiver(ReceiveSettings settings, MessageWrapperSerializer serializer, Action<string, Exception> criticalErrorAction)
        {
            var unwrapper = MessageUnwrapper != null
                ? (IMessageEnvelopeUnwrapper)new UserProvidedEnvelopeUnwrapper(MessageUnwrapper)
                : new DefaultMessageEnvelopeUnwrapper(serializer);

            var receiver = new AzureMessageQueueReceiver(unwrapper, queueServiceClientProvider, GetQueueAddressGenerator(), settings.PurgeOnStartup, MessageInvisibleTime);

            return new MessageReceiver(
                settings.Id,
                TransportTransactionMode,
                receiver,
                settings.ReceiveAddress,
                settings.ErrorQueue,
                criticalErrorAction,
                DegreeOfReceiveParallelism,
                ReceiverBatchSize,
                MaximumWaitTimeWhenIdle,
                PeekInterval);
        }

        QueueAddressGenerator GetQueueAddressGenerator()
        {
            if (queueAddressGenerator == null)
            {
                queueAddressGenerator = new QueueAddressGenerator(QueueNameSanitizer);
            }

            return queueAddressGenerator;
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

            return GetQueueAddressGenerator().GetQueueName(queue.ToString());
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
                    throw new ArgumentOutOfRangeException(nameof(ReceiverBatchSize), value, "Batch size must be between 1 and 32 messages.");
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
        public SerializationDefinition MessageWrapperSerializationDefinition { get; set; }

        /// <summary>
        /// Registers a custom unwrapper to convert native messages to <see cref="MessageWrapper" />. This is needed when receiving raw json/xml/etc messages from non NServiceBus endpoints.
        /// </summary>
        public Func<QueueMessage, MessageWrapper> MessageUnwrapper
        {
            get => messageUnwrapper;
            set
            {
                Guard.AgainstNull(nameof(MessageUnwrapper), value);
                messageUnwrapper = value;
            }
        }

        /// <summary>
        /// Provides options to define settings for the transport DelayedDelivery feature.
        /// </summary>
        public NativeDelayedDeliverySettings DelayedDelivery { get; } = new NativeDelayedDeliverySettings();

        /// <summary>
        /// Define routing between Azure Storage accounts and map them to a logical alias instead of using bare
        /// connection strings.
        /// </summary>
        public AccountRoutingSettings AccountRouting { get; } = new AccountRoutingSettings();

        internal const string SerializerSettingsKey = "MainSerializer";
        readonly TransportTransactionMode[] supportedTransactionModes = new[] { TransportTransactionMode.None, TransportTransactionMode.ReceiveOnly };
        TimeSpan messageInvisibleTime = TimeSpan.FromSeconds(30);
        TimeSpan peekInterval = TimeSpan.FromMilliseconds(125);
        TimeSpan maximumWaitTimeWhenIdle = TimeSpan.FromSeconds(30);
        Func<string, string> queueNameSanitizer = entityName => entityName;
        QueueAddressGenerator queueAddressGenerator;
        IQueueServiceClientProvider queueServiceClientProvider;
        IBlobServiceClientProvider blobServiceClientProvider;
        ICloudTableClientProvider cloudTableClientProvider;
        int? receiverBatchSize;
        int? degreeOfReceiveParallelism;
        Func<QueueMessage, MessageWrapper> messageUnwrapper;
    }
}