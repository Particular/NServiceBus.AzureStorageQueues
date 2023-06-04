namespace NServiceBus
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Transports.WindowsAzureStorageQueues;
    using global::Azure.Storage.Blobs;
    using global::Azure.Storage.Queues;
    using global::Azure.Storage.Queues.Models;
    using MessageInterfaces;
    using Microsoft.Azure.Cosmos.Table;
    using Routing;
    using Serialization;
    using Settings;
    using Transport;
    using Transport.AzureStorageQueues;
    using Unicast.Messages;

    /// <summary>
    /// Transport definition for AzureStorageQueue
    /// </summary>
    public class AzureStorageQueueTransport : TransportDefinition, IMessageDrivenSubscriptionTransport
    {
        internal AzureStorageQueueTransport()
            : base(TransportTransactionMode.ReceiveOnly, supportsDelayedDelivery: true, supportsPublishSubscribe: false, supportsTTBR: true)
        {

        }

        internal void LegacyAPIShimSetConnectionString(string connectionString)
        {
            Guard.AgainstNullAndEmpty(nameof(connectionString), connectionString);

            queueServiceClientProvider = new QueueServiceClientByConnectionString(connectionString);
            if (SupportsDelayedDelivery)
            {
                blobServiceClientProvider = new BlobServiceClientProvidedByConnectionString(connectionString);
                cloudTableClientProvider = new CloudTableClientByConnectionString(connectionString);
            }
        }

        /// <summary>
        /// Initialize a new transport definition for AzureStorageQueue
        /// </summary>
        public AzureStorageQueueTransport(string connectionString, bool useNativeDelayedDeliveries = true)
            : base(TransportTransactionMode.ReceiveOnly, supportsDelayedDelivery: useNativeDelayedDeliveries, supportsPublishSubscribe: true, supportsTTBR: true)
        {
            Guard.AgainstNullAndEmpty(nameof(connectionString), connectionString);

            queueServiceClientProvider = new QueueServiceClientByConnectionString(connectionString);

            if (SupportsDelayedDelivery || SupportsPublishSubscribe)
            {
                cloudTableClientProvider = new CloudTableClientByConnectionString(connectionString);
            }

            if (SupportsDelayedDelivery)
            {
                blobServiceClientProvider = new BlobServiceClientProvidedByConnectionString(connectionString);
            }
        }

        /// <summary>
        /// Initialize a new transport definition for AzureStorageQueue and disable native delayed deliveries
        /// </summary>
        public AzureStorageQueueTransport(QueueServiceClient queueServiceClient)
            : base(TransportTransactionMode.ReceiveOnly, supportsDelayedDelivery: false, supportsPublishSubscribe: true, supportsTTBR: true)
        {
            Guard.AgainstNull(nameof(queueServiceClient), queueServiceClient);

            queueServiceClientProvider = new QueueServiceClientProvidedByUser(queueServiceClient);
        }

        /// <summary>
        /// Initialize a new transport definition for AzureStorageQueue with native delayed deliveries support
        /// </summary>
        public AzureStorageQueueTransport(QueueServiceClient queueServiceClient, BlobServiceClient blobServiceClient, CloudTableClient cloudTableClient)
            : base(TransportTransactionMode.ReceiveOnly, supportsDelayedDelivery: true, supportsPublishSubscribe: true, supportsTTBR: true)
        {
            Guard.AgainstNull(nameof(queueServiceClient), queueServiceClient);
            Guard.AgainstNull(nameof(blobServiceClient), blobServiceClient);
            Guard.AgainstNull(nameof(cloudTableClient), cloudTableClient);

            queueServiceClientProvider = new QueueServiceClientProvidedByUser(queueServiceClient);
            blobServiceClientProvider = new BlobServiceClientProvidedByUser(blobServiceClient);
            cloudTableClientProvider = new CloudTableClientProvidedByUser(cloudTableClient);
        }

        /// <summary>
        /// For the pub-sub migration tests only
        /// </summary>
        internal AzureStorageQueueTransport(string connectionString, bool supportsDelayedDelivery, bool supportsPublishSubscribe)
            : base(TransportTransactionMode.ReceiveOnly, supportsDelayedDelivery, supportsPublishSubscribe, true)
        {
            Guard.AgainstNullAndEmpty(nameof(connectionString), connectionString);

            queueServiceClientProvider = new QueueServiceClientByConnectionString(connectionString);

            if (SupportsDelayedDelivery || SupportsPublishSubscribe)
            {
                cloudTableClientProvider = new CloudTableClientByConnectionString(connectionString);
            }

            if (SupportsDelayedDelivery)
            {
                blobServiceClientProvider = new BlobServiceClientProvidedByConnectionString(connectionString);
            }
        }

        static string GenerateDelayedDeliveryTableName(string endpointName)
        {
            byte[] hashedName;
            using (var sha1 = SHA1.Create())
            {
                sha1.Initialize();
                hashedName = sha1.ComputeHash(Encoding.UTF8.GetBytes(endpointName));
            }

            var hashName = BitConverter.ToString(hashedName).Replace("-", string.Empty);
            return "delays" + hashName.ToLower();
        }

        /// <inheritdoc cref="Initialize"/>
        public override async Task<TransportInfrastructure> Initialize(HostSettings hostSettings, ReceiveSettings[] receiversSettings, string[] sendingAddresses, CancellationToken cancellationToken = default)
        {
            if (queueServiceClientProvider == null)
            {
                //legacy shim API guard: if queueServiceClientProvider is null it means that ConnectionString() has not been invoked
                throw new Exception("Cannot initialize the transport without a valid connection " +
                                    "string or a configured QueueServiceClient. If using the obsoleted API to " +
                                    "configure the transport, make sure to call transportConfig.ConnectionString() " +
                                    "to configure the client connection string.");
            }

            Guard.AgainstNull(nameof(hostSettings), hostSettings);
            Guard.AgainstNull(nameof(receiversSettings), receiversSettings);
            Guard.AgainstNull(nameof(sendingAddresses), sendingAddresses);

            ValidateReceiversSettings(receiversSettings);

            var localAccountInfo = new AccountInfo("", queueServiceClientProvider.Client, cloudTableClientProvider.Client);

            var azureStorageAddressing = new AzureStorageAddressingSettings(GetQueueAddressGenerator(),
                AccountRouting.DefaultAccountAlias,
                Subscriptions.SubscriptionTableName,
                AccountRouting.Mappings,
                localAccountInfo);

            object delayedDeliveryPersistenceDiagnosticSection = new { };
            CloudTable delayedMessagesStorageTable = null;
            var nativeDelayedDeliveryPersistence = NativeDelayDeliveryPersistence.Disabled();
            if (SupportsDelayedDelivery)
            {
                delayedMessagesStorageTable = await EnsureNativeDelayedDeliveryTable(
                    hostSettings.Name,
                    DelayedDelivery.DelayedDeliveryTableName,
                    cloudTableClientProvider.Client,
                    hostSettings.SetupInfrastructure,
                    cancellationToken).ConfigureAwait(false);

                nativeDelayedDeliveryPersistence = new NativeDelayDeliveryPersistence(delayedMessagesStorageTable);

                delayedDeliveryPersistenceDiagnosticSection = new
                {
                    DelayedDeliveryTableName = delayedMessagesStorageTable.Name,
                    UserDefinedDelayedDeliveryTableName = !string.IsNullOrWhiteSpace(DelayedDelivery.DelayedDeliveryTableName)
                };
            }

            var serializerSettingsHolder = hostSettings.CoreSettings;
            if (serializerSettingsHolder == null)
            {
                //in raw transport mode to set up the required serializer a settings holder
                //is needed to store MessageMetadataRegistry and Conventions instances.
                //https://github.com/Particular/NServiceBus.AzureStorageQueues/issues/524

                var tempSettingsHolder = new SettingsHolder();
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.CreateInstance;
                var conventions = tempSettingsHolder.GetOrCreate<Conventions>();
                var registry = (MessageMetadataRegistry)Activator.CreateInstance(typeof(MessageMetadataRegistry), flags, null, new object[] { new Func<Type, bool>(t => conventions.IsMessageType(t)), true }, CultureInfo.InvariantCulture);

                tempSettingsHolder.Set(registry);
                serializerSettingsHolder = tempSettingsHolder;
            }

            var serializer = BuildSerializer(MessageWrapperSerializationDefinition, serializerSettingsHolder);

            object subscriptionsPersistenceDiagnosticSection = new { };
            ISubscriptionStore subscriptionStore = new NoOpSubscriptionStore();
            if (SupportsPublishSubscribe)
            {
                var subscriptionTable = await EnsureSubscriptionTableExists(cloudTableClientProvider.Client, Subscriptions.SubscriptionTableName, hostSettings.SetupInfrastructure, cancellationToken)
                    .ConfigureAwait(false);

                object subscriptionPersistenceCachingSection = new { IsEnabled = false };

                subscriptionStore = new SubscriptionStore(azureStorageAddressing);

                if (Subscriptions.DisableCaching == false)
                {
                    subscriptionPersistenceCachingSection = new
                    {
                        IsEnabled = true,
                        Subscriptions.CacheInvalidationPeriod
                    };

                    subscriptionStore = new CachedSubscriptionStore(subscriptionStore, Subscriptions.CacheInvalidationPeriod);
                }

                subscriptionsPersistenceDiagnosticSection = new
                {
                    SubscriptionTableName = subscriptionTable.Name,
                    UserDefinedSubscriptionTableName = !string.IsNullOrWhiteSpace(Subscriptions.SubscriptionTableName),
                    Caching = subscriptionPersistenceCachingSection
                };
            }

            var dispatcher = new Dispatcher(GetQueueAddressGenerator(), azureStorageAddressing, serializer, nativeDelayedDeliveryPersistence, subscriptionStore);

            object delayedDeliveryProcessorDiagnosticSection = new { };
            var nativeDelayedDeliveryProcessor = NativeDelayedDeliveryProcessor.Disabled();
            if (SupportsDelayedDelivery)
            {
                var nativeDelayedDeliveryErrorQueue = DelayedDelivery.DelayedDeliveryPoisonQueue
                    ?? hostSettings.CoreSettings?.GetOrDefault<string>(ErrorQueueSettings.SettingsKey)
                    ?? receiversSettings.Select(settings => settings.ErrorQueue).FirstOrDefault();

                nativeDelayedDeliveryProcessor = new NativeDelayedDeliveryProcessor(
                        dispatcher,
                        delayedMessagesStorageTable,
                        blobServiceClientProvider.Client,
                        nativeDelayedDeliveryErrorQueue,
                        TransportTransactionMode,
                        new BackoffStrategy(PeekInterval, MaximumWaitTimeWhenIdle));
                nativeDelayedDeliveryProcessor.Start(cancellationToken);

                delayedDeliveryProcessorDiagnosticSection = new
                {
                    DelayedDeliveryPoisonQueue = nativeDelayedDeliveryErrorQueue,
                    UserDefinedDelayedDeliveryPoisonQueue = !string.IsNullOrWhiteSpace(DelayedDelivery.DelayedDeliveryPoisonQueue)
                };
            }

            var messageReceivers = receiversSettings.Select(settings => BuildReceiver(hostSettings, settings,
                    serializer,
                    hostSettings.CriticalErrorAction,
                    subscriptionStore))
                .ToDictionary(receiver => receiver.Id, receiver => receiver.Receiver);

            if (hostSettings.SetupInfrastructure)
            {
                var queuesToCreate = messageReceivers.Select(settings => settings.Value.ReceiveAddress).Union(sendingAddresses).ToList();
                if (SupportsDelayedDelivery && !string.IsNullOrWhiteSpace(DelayedDelivery.DelayedDeliveryPoisonQueue))
                {
                    queuesToCreate.Add(DelayedDelivery.DelayedDeliveryPoisonQueue);
                }

                var queueCreator = new AzureMessageQueueCreator(queueServiceClientProvider, GetQueueAddressGenerator());
                await queueCreator.CreateQueueIfNecessary(queuesToCreate, cancellationToken)
                    .ConfigureAwait(false);
            }

            var infrastructure = new AzureStorageQueueInfrastructure(
                this,
                dispatcher,
                new ReadOnlyDictionary<string, IMessageReceiver>(messageReceivers),
                nativeDelayedDeliveryProcessor);

            hostSettings.StartupDiagnostic.Add("NServiceBus.Transport.AzureStorageQueues", new
            {
                ConnectionMechanism = new
                {
                    Queue = queueServiceClientProvider is QueueServiceClientByConnectionString ? "ConnectionString" : "QueueServiceClient",
                    Table = cloudTableClientProvider is CloudTableClientByConnectionString ? "ConnectionString" : "CloudTableClient",
                    Blob = blobServiceClientProvider is BlobServiceClientProvidedByConnectionString ? "ConnectionString" : "BlobServiceClient",
                },
                MessageWrapperSerializer = MessageWrapperSerializationDefinition == null ? "Default" : "Custom",
                MessageEnvelopeUnwrapper = MessageUnwrapper == null ? "Default" : "Custom",
                NativeDelayedDelivery = new
                {
                    IsEnabled = SupportsDelayedDelivery,
                    Processor = delayedDeliveryProcessorDiagnosticSection,
                    Persistence = delayedDeliveryPersistenceDiagnosticSection
                },
                Subscriptions = new
                {
                    IsEnabled = SupportsPublishSubscribe,
                    Persistence = subscriptionsPersistenceDiagnosticSection
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
                throw new Exception("Send only endpoints require a native delayed poison queue." +
                                    " Configure a user defined poison queue for delayed deliveries by using the" +
                                    $" {nameof(AzureStorageQueueTransport)}.{nameof(DelayedDelivery)}" +
                                    $".{nameof(DelayedDelivery.DelayedDeliveryPoisonQueue)} property.");
            }
        }

        static async Task<CloudTable> EnsureNativeDelayedDeliveryTable(string endpointName, string delayedDeliveryTableName, CloudTableClient cloudTableClient, bool setupInfrastructure, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(delayedDeliveryTableName))
            {
                delayedDeliveryTableName = GenerateDelayedDeliveryTableName(endpointName);
            }

            var delayedMessagesStorageTable = cloudTableClient.GetTableReference(delayedDeliveryTableName);
            if (setupInfrastructure)
            {
                await delayedMessagesStorageTable.CreateIfNotExistsAsync(cancellationToken).ConfigureAwait(false);
            }

            return delayedMessagesStorageTable;
        }

        static async Task<CloudTable> EnsureSubscriptionTableExists(CloudTableClient cloudTableClient, string subscriptionTableName, bool setupInfrastructure, CancellationToken cancellationToken)
        {
            var subscriptionTable = cloudTableClient.GetTableReference(subscriptionTableName);
            if (setupInfrastructure)
            {
                await subscriptionTable.CreateIfNotExistsAsync(cancellationToken).ConfigureAwait(false);
            }

            return subscriptionTable;
        }

        static MessageWrapperSerializer BuildSerializer(SerializationDefinition userWrapperSerializationDefinition, IReadOnlySettings settings)
        {
            return userWrapperSerializationDefinition != null
                ? new MessageWrapperSerializer(userWrapperSerializationDefinition.Configure(settings).Invoke(MessageWrapperSerializer.GetMapper()))
                : new MessageWrapperSerializer(GetMainSerializerHack(MessageWrapperSerializer.GetMapper(), settings));
        }

        internal static IMessageSerializer GetMainSerializerHack(IMessageMapper mapper, IReadOnlySettings settings)
        {
            if (!settings.TryGet<Tuple<SerializationDefinition, SettingsHolder>>(SerializerSettingsKey, out var serializerSettingsTuple))
            {
                throw new Exception("No serializer defined. If the transport is used in combination with NServiceBus, " +
                                    "use 'endpointConfiguration.UseSerialization<T>();' to select a serializer. " +
                                    "If you are upgrading, install the `NServiceBus.Newtonsoft.Json` NuGet package " +
                                    "and consult the upgrade guide for further information. If the transport is used in isolation, " +
                                    "set a HostSettings.CoreSettings to an empty SettingsHolder instance before starting the transport.");
            }

            var (definition, _) = serializerSettingsTuple;

            var serializerFactory = definition.Configure(settings);
            var serializer = serializerFactory(mapper);
            return serializer;
        }

        (string Id, IMessageReceiver Receiver) BuildReceiver(HostSettings hostSettings, ReceiveSettings receiveSettings,
            MessageWrapperSerializer serializer,
            Action<string, Exception, CancellationToken> criticalErrorAction, ISubscriptionStore subscriptionStore)
        {
            var unwrapper = MessageUnwrapper != null
                ? (IMessageEnvelopeUnwrapper)new UserProvidedEnvelopeUnwrapper(MessageUnwrapper)
                : new DefaultMessageEnvelopeUnwrapper(serializer);

#pragma warning disable CS0618 // Type or member is obsolete
            var receiveAddress = ToTransportAddress(receiveSettings.ReceiveAddress);
#pragma warning restore CS0618 // Type or member is obsolete

            var subscriptionManager = new SubscriptionManager(subscriptionStore, hostSettings.Name, receiveAddress);

            var receiver = new AzureMessageQueueReceiver(unwrapper, queueServiceClientProvider, GetQueueAddressGenerator(), serializer, receiveSettings.PurgeOnStartup, MessageInvisibleTime);

            return (receiveSettings.Id, new MessageReceiver(
                receiveSettings.Id,
                TransportTransactionMode,
                receiver,
                subscriptionManager,
                receiveAddress,
                receiveSettings.ErrorQueue,
                criticalErrorAction,
                DegreeOfReceiveParallelism,
                ReceiverBatchSize,
                MaximumWaitTimeWhenIdle,
                PeekInterval));
        }

        QueueAddressGenerator GetQueueAddressGenerator()
        {
            queueAddressGenerator ??= new QueueAddressGenerator(QueueNameSanitizer);

            return queueAddressGenerator;
        }

        /// <inheritdoc cref="ToTransportAddress"/>
        [ObsoleteEx(Message = "Inject the ITransportAddressResolver type to access the address translation mechanism at runtime. See the NServiceBus version 8 upgrade guide for further details.",
            TreatAsErrorFromVersion = "12",
            RemoveInVersion = "13")]
#pragma warning disable CS0672 // Member overrides obsolete member
        public override string ToTransportAddress(Transport.QueueAddress address)
#pragma warning restore CS0672 // Member overrides obsolete member
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
                    catch (Exception ex)
                    {
                        throw new Exception("Registered queue name sanitizer threw an exception.", ex);
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
                if (value is < 1 or > 32)
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

                if (degreeOfReceiveParallelism is < 1 or > maxDegreeOfReceiveParallelism)
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
        /// Provides options to define settings for the transport subscription feature.
        /// </summary>
        public SubscriptionSettings Subscriptions { get; } = new SubscriptionSettings();

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