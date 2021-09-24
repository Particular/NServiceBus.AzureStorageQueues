namespace NServiceBus.Transport.AzureStorageQueues
{
    using System;
    using System.Globalization;
    using System.Collections.Generic;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading.Tasks;
    using DelayedDelivery;
    using Features;
    using Logging;
    using Microsoft.Azure.Cosmos.Table;
    using Performance.TimeToBeReceived;
    using Routing;
    using Serialization;
    using Settings;
    using Transport;

    class AzureStorageQueueInfrastructure : TransportInfrastructure
    {
        internal AzureStorageQueueInfrastructure(SettingsHolder settings, string connectionString)
        {
            this.settings = settings;
            this.connectionString = connectionString;

            if (connectionString != null && IsPremiumEndpoint(this.connectionString))
            {
                throw new Exception($"When configuring {nameof(AzureStorageQueueTransport)} with a single connection string, only Azure Storage connection can be used. See documentation for alternative options to configure the transport.");
            }

            if (settings.HasSetting(WellKnownConfigurationKeys.PubSub.DisablePublishSubscribe))
            {
                OutboundRoutingPolicy = new OutboundRoutingPolicy(OutboundRoutingType.Unicast, OutboundRoutingType.Unicast, OutboundRoutingType.Unicast);
            }
            else
            {
                OutboundRoutingPolicy = new OutboundRoutingPolicy(OutboundRoutingType.Unicast, OutboundRoutingType.Multicast, OutboundRoutingType.Unicast);
            }

            serializer = BuildSerializer(settings, out var userProvidedSerializer);

            settings.SetDefault(WellKnownConfigurationKeys.DelayedDelivery.EnableTimeoutManager, true);

            if (!settings.IsFeatureEnabled(typeof(TimeoutManager)) || settings.GetOrDefault<bool>("Endpoint.SendOnly"))
            {
                // TimeoutManager is already not used. Indicate to Native Delayed Delivery that we're not in the hybrid mode.
                settings.Set(WellKnownConfigurationKeys.DelayedDelivery.EnableTimeoutManager, false);
            }

            if (!settings.TryGet(out queueServiceClientProvider))
            {
                if (connectionString == null)
                {
                    throw new Exception($"Unable to connect to queue service. Supply a queue service client (`transport.{nameof(AzureStorageTransportExtensions.UseQueueServiceClient)}(...)`) or configure a connection string (`transport.{nameof(TransportExtensions<AzureStorageQueueTransport>.ConnectionString)}(...)`).");
                }
                queueServiceClientProvider = new QueueServiceClientProvidedByConnectionString(connectionString);
            }

            if (PublishSubscribeIsEnabled() || NativeDelayedDeliveryIsEnabled())
            {
                if (!settings.TryGet(out cloudTableClientProvider))
                {
                    if (connectionString == null)
                    {
                        throw new Exception($"Unable to connect to cloud table service. Supply a cloud table service client (`transport.{nameof(AzureStorageTransportExtensions.UseCloudTableClient)}(...)`) or configure a connection string (`transport.{nameof(TransportExtensions<AzureStorageQueueTransport>.ConnectionString)}(...)`).");
                    }
                    cloudTableClientProvider = new CloudTableClientProvidedByConnectionString(this.connectionString);
                }
            }

            maximumWaitTime = settings.Get<TimeSpan>(WellKnownConfigurationKeys.ReceiverMaximumWaitTimeWhenIdle);
            peekInterval = settings.Get<TimeSpan>(WellKnownConfigurationKeys.ReceiverPeekInterval);
            messageInvisibleTime = settings.Get<TimeSpan>(WellKnownConfigurationKeys.ReceiverMessageInvisibleTime);

            addressGenerator = new QueueAddressGenerator(settings.GetOrDefault<Func<string, string>>(WellKnownConfigurationKeys.QueueSanitizer));

            addressing = GetAddressing();

            var delayedDeliveryIsEnabled = NativeDelayedDeliveryIsEnabled();

            object delayedDelivery;
            if (delayedDeliveryIsEnabled)
            {
                if (!settings.TryGet(out IProvideBlobServiceClient blobServiceClientProvider))
                {
                    if (connectionString == null)
                    {
                        throw new Exception($"Unable to connect to blob service. Supply a blob service client (`transport.{nameof(AzureStorageTransportExtensions.UseBlobServiceClient)}(...)`) or configure a connection string (`transport.{nameof(TransportExtensions<AzureStorageQueueTransport>.ConnectionString)}(...)`).");
                    }
                    blobServiceClientProvider = new BlobServiceClientProvidedByConnectionString(this.connectionString);
                }

                // This call mutates settings holder and should not be invoked more than once. This value is used for Diagnostics Section upon startup as well.
                var delayedDeliveryTableName = GetDelayedDeliveryTableName(settings);

                nativeDelayedDelivery = new NativeDelayDelivery(
                    cloudTableClientProvider,
                    blobServiceClientProvider,
                    delayedDeliveryTableName,
                    settings.ErrorQueueAddress(),
                    GetRequiredTransactionMode(),
                    maximumWaitTime,
                    peekInterval,
                    BuildDispatcher);

                delayedDelivery = new
                {
                    NativeDelayedDeliveryIsEnabled = true,
                    NativeDelayedDeliveryTableName = delayedDeliveryTableName,
                    TimeoutManagerEnabled = this.settings.Get<bool>(WellKnownConfigurationKeys.DelayedDelivery.EnableTimeoutManager)
                };
            }
            else
            {
                nativeDelayedDelivery = new DisabledNativeDelayDelivery();

                delayedDelivery = new
                {
                    NativeDelayedDeliveryIsEnabled = false,
                    TimeoutManagerEnabled = this.settings.Get<bool>(WellKnownConfigurationKeys.DelayedDelivery.EnableTimeoutManager)
                };
            }

            var subscriptionTableName = GetSubscriptionTableName(settings);

            object publishSubscribe = new { NativePublishSubscribeIsEnabled = false, };
            subscriptionStore = new NoOpSubscriptionStore();
            if (PublishSubscribeIsEnabled())
            {
                object subscriptionPersistenceCachingSection = new { IsEnabled = false };

                subscriptionStore = new SubscriptionStore(addressing);

                if (settings.Get<bool>(WellKnownConfigurationKeys.PubSub.DisableCaching) == false)
                {
                    var cacheInvalidationPeriod = settings.Get<TimeSpan>(WellKnownConfigurationKeys.PubSub.CacheInvalidationPeriod);

                    subscriptionPersistenceCachingSection = new
                    {
                        IsEnabled = true,
                        CacheInvalidationPeriod = cacheInvalidationPeriod
                    };

                    subscriptionStore = new CachedSubscriptionStore(subscriptionStore, cacheInvalidationPeriod);
                }

                publishSubscribe = new
                {
                    NativePublishSubscribeIsEnabled = true,
                    SubscriptionTableName = subscriptionTableName,
                    Caching = subscriptionPersistenceCachingSection
                };
            }

            settings.AddStartupDiagnosticsSection("NServiceBus.Transport.AzureStorageQueues", new
            {
                ConnectionMechanism = new
                {
                    Queue = this.settings.TryGet<IProvideQueueServiceClient>(out _) ? "QueueServiceClient" : "ConnectionString",
                    Table = this.settings.TryGet<IProvideCloudTableClient>(out _) ? "CloudTableClient" : "ConnectionString",
                    Blob = this.settings.TryGet<IProvideBlobServiceClient>(out _) ? "BlobServiceClient" : "ConnectionString",
                },
                MessageWrapperSerializer = userProvidedSerializer ? "Custom" : "Default",
                MessageEnvelopeUnwrapper = settings.HasExplicitValue<IMessageEnvelopeUnwrapper>() ? "Custom" : "Default",
                DelayedDelivery = delayedDelivery,
                PublishSubscribe = publishSubscribe,
                TransactionMode = Enum.GetName(typeof(TransportTransactionMode), GetRequiredTransactionMode()),
                ReceiverBatchSize = this.settings.TryGet(WellKnownConfigurationKeys.ReceiverBatchSize, out int receiverBatchSize) ? receiverBatchSize.ToString(CultureInfo.InvariantCulture) : "Default",
                DegreeOfReceiveParallelism = this.settings.TryGet(WellKnownConfigurationKeys.DegreeOfReceiveParallelism, out int degreeOfReceiveParallelism) ? degreeOfReceiveParallelism.ToString(CultureInfo.InvariantCulture) : "Default",
                MaximumWaitTimeWhenIdle = maximumWaitTime,
                PeekInterval = peekInterval,
                MessageInvisibleTime = messageInvisibleTime
            });
        }

        // the SDK uses similar method of changing the underlying executor
        static bool IsPremiumEndpoint(string connectionString)
        {
            var lowerInvariant = connectionString.ToLowerInvariant();
            return lowerInvariant.Contains("https://localhost") || lowerInvariant.Contains(".table.cosmosdb.") || lowerInvariant.Contains(".table.cosmos.");
        }

        public override IEnumerable<Type> DeliveryConstraints => new List<Type> { typeof(DiscardIfNotReceivedBefore), typeof(NonDurableDelivery), typeof(DoNotDeliverBefore), typeof(DelayDeliveryWith) };

        public override TransportTransactionMode TransactionMode { get; } = TransportTransactionMode.ReceiveOnly;
        public override OutboundRoutingPolicy OutboundRoutingPolicy { get; }

        static string GetDelayedDeliveryTableName(SettingsHolder settings)
        {
            var delayedDeliveryTableName = settings.GetOrDefault<string>(WellKnownConfigurationKeys.DelayedDelivery.TableName);
            var delayedDeliveryTableNameWasNotOverridden = string.IsNullOrEmpty(delayedDeliveryTableName);

            if (delayedDeliveryTableNameWasNotOverridden)
            {
                delayedDeliveryTableName = GenerateDelayedDeliveryTableName(settings.EndpointName());
                settings.Set(WellKnownConfigurationKeys.DelayedDelivery.TableName, delayedDeliveryTableName);
            }

            return delayedDeliveryTableName;
        }

        static string GetSubscriptionTableName(ReadOnlySettings settings)
        {
            return settings.GetOrDefault<string>(WellKnownConfigurationKeys.PubSub.TableName);
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

        bool NativeDelayedDeliveryIsEnabled() => settings.GetOrDefault<bool>(WellKnownConfigurationKeys.DelayedDelivery.DisableDelayedDelivery) == false;

        bool PublishSubscribeIsEnabled() => settings.GetOrDefault<bool>(WellKnownConfigurationKeys.PubSub.DisablePublishSubscribe) == false;

        public override TransportReceiveInfrastructure ConfigureReceiveInfrastructure()
        {
            Logger.Debug("Configuring receive infrastructure");

            return new TransportReceiveInfrastructure(
                () =>
                {
                    var unwrapper = settings.HasSetting<IMessageEnvelopeUnwrapper>() ? settings.GetOrDefault<IMessageEnvelopeUnwrapper>() : new DefaultMessageEnvelopeUnwrapper(serializer);

                    var receiver = new AzureMessageQueueReceiver(unwrapper, queueServiceClientProvider, addressGenerator)
                    {
                        MessageInvisibleTime = messageInvisibleTime,
                    };

                    int? degreeOfReceiveParallelism = null;
                    if (settings.TryGet<int>(WellKnownConfigurationKeys.DegreeOfReceiveParallelism, out var parallelism))
                    {
                        degreeOfReceiveParallelism = parallelism;
                    }

                    int? batchSize = null;
                    if (settings.TryGet<int>(WellKnownConfigurationKeys.ReceiverBatchSize, out var size))
                    {
                        batchSize = size;
                    }

                    return new MessagePump(receiver, degreeOfReceiveParallelism, batchSize, maximumWaitTime, peekInterval);
                },
                () => new AzureMessageQueueCreator(queueServiceClientProvider, addressGenerator),
                () => Task.FromResult(StartupCheckResult.Success)
            );
        }

        static MessageWrapperSerializer BuildSerializer(ReadOnlySettings settings, out bool userProvided)
        {
            if (settings.TryGet<SerializationDefinition>(WellKnownConfigurationKeys.MessageWrapperSerializationDefinition, out var wrapperSerializer))
            {
                userProvided = true;
                return new MessageWrapperSerializer(wrapperSerializer.Configure(settings)(MessageWrapperSerializer.GetMapper()));
            }

            userProvided = false;
            return new MessageWrapperSerializer(AzureStorageQueueTransport.GetMainSerializer(MessageWrapperSerializer.GetMapper(), settings));
        }

        public override TransportSendInfrastructure ConfigureSendInfrastructure()
        {
            return new TransportSendInfrastructure(BuildDispatcher, () => Task.FromResult(NativeDelayDelivery.CheckForInvalidSettings(settings)));
        }

        Dispatcher BuildDispatcher()
        {
            return new Dispatcher(addressGenerator, addressing, serializer, nativeDelayedDelivery.ShouldDispatch, subscriptionStore);
        }

        AzureStorageAddressingSettings GetAddressing()
        {
            const string defaultAccountAlias = "";

            if (settings.TryGet<AccountConfigurations>(out var accounts) == false)
            {
                accounts = new AccountConfigurations();
            }

            var localAccountInfo = new AccountInfo(defaultAccountAlias, queueServiceClientProvider.Client, cloudTableClientProvider?.Client);
            var addressingSettings = new AzureStorageAddressingSettings(addressGenerator, accounts.defaultAlias ?? defaultAccountAlias, GetSubscriptionTableName(settings), accounts.mappings, localAccountInfo);
            return addressingSettings;
        }

        public override TransportSubscriptionInfrastructure ConfigureSubscriptionInfrastructure()
        {
            return new TransportSubscriptionInfrastructure(() => new SubscriptionManager(subscriptionStore, settings.EndpointName(), settings.LocalAddress()));
        }

        public override EndpointInstance BindToLocalEndpoint(EndpointInstance instance)
        {
            return instance;
        }

        public override string ToTransportAddress(LogicalAddress logicalAddress)
        {
            var queue = new StringBuilder(logicalAddress.EndpointInstance.Endpoint);

            if (logicalAddress.EndpointInstance.Discriminator != null)
            {
                queue.Append("-" + logicalAddress.EndpointInstance.Discriminator);
            }

            if (logicalAddress.Qualifier != null)
            {
                queue.Append("-" + logicalAddress.Qualifier);
            }

            return addressGenerator.GetQueueName(queue.ToString());
        }

        public override async Task Start()
        {
            if (PublishSubscribeIsEnabled())
            {
                await EnsureSubscriptionTableExists(cloudTableClientProvider.Client, GetSubscriptionTableName(settings)).ConfigureAwait(false);
            }
            await nativeDelayedDelivery.Start().ConfigureAwait(false);
        }

        static async Task EnsureSubscriptionTableExists(CloudTableClient cloudTableClient, string subscriptionTableName)
        {
            var subscriptionTable = cloudTableClient.GetTableReference(subscriptionTableName);
            await subscriptionTable.CreateIfNotExistsAsync().ConfigureAwait(false);
        }

        public override Task Stop()
        {
            return nativeDelayedDelivery.Stop();
        }

        TransportTransactionMode GetRequiredTransactionMode()
        {
            var transportTransactionSupport = TransactionMode;

            //if user haven't asked for a explicit level use what the transport supports
            if (!settings.TryGet(out TransportTransactionMode requestedTransportTransactionMode))
            {
                return transportTransactionSupport;
            }

            if (requestedTransportTransactionMode > transportTransactionSupport)
            {
                throw new Exception($"Requested transaction mode `{requestedTransportTransactionMode}` can't be satisfied since the transport only supports `{transportTransactionSupport}`");
            }

            return requestedTransportTransactionMode;
        }

        static async Task<CloudTable> EnsureSubscriptionTableExists(CloudTableClient cloudTableClient, string subscriptionTableName, bool setupInfrastructure)
        {
            var subscriptionTable = cloudTableClient.GetTableReference(subscriptionTableName);
            if (setupInfrastructure)
            {
                await subscriptionTable.CreateIfNotExistsAsync().ConfigureAwait(false);
            }

            return subscriptionTable;
        }

        readonly ReadOnlySettings settings;
        readonly string connectionString;
        readonly MessageWrapperSerializer serializer;
        IProvideCloudTableClient cloudTableClientProvider;
        IProvideQueueServiceClient queueServiceClientProvider;
        INativeDelayDelivery nativeDelayedDelivery;
        QueueAddressGenerator addressGenerator;
        ISubscriptionStore subscriptionStore;
        AzureStorageAddressingSettings addressing;
        readonly TimeSpan maximumWaitTime;
        readonly TimeSpan peekInterval;
        readonly TimeSpan messageInvisibleTime;

        static readonly ILog Logger = LogManager.GetLogger<AzureStorageQueueInfrastructure>();
    }
}