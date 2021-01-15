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

            if (IsPremiumEndpoint(this.connectionString))
            {
                throw new Exception($"When configuring {nameof(AzureStorageQueueTransport)} with a single connection string, only Azure Storage connection can be used. See documentation for alternative options to configure the transport.");
            }

            serializer = BuildSerializer(settings, out var userProvidedSerializer);

            settings.SetDefault(WellKnownConfigurationKeys.DelayedDelivery.EnableTimeoutManager, true);

            if (!settings.IsFeatureEnabled(typeof(TimeoutManager)) || settings.GetOrDefault<bool>("Endpoint.SendOnly"))
            {
                // TimeoutManager is already not used. Indicate to Native Delayed Delivery that we're not in the hybrid mode.
                settings.Set(WellKnownConfigurationKeys.DelayedDelivery.EnableTimeoutManager, false);
            }

            if (!settings.TryGet<IProvideCloudTableClient>(out var cloudTableClientProvider))
            {
                cloudTableClientProvider = new CloudTableClientProvidedByConnectionString(this.connectionString);
            }

            if (!settings.TryGet<IProvideBlobServiceClient>(out var blobServiceClientProvider))
            {
                blobServiceClientProvider = new BlobServiceClientProvidedByConnectionString(this.connectionString);
            }

            maximumWaitTime = settings.Get<TimeSpan>(WellKnownConfigurationKeys.ReceiverMaximumWaitTimeWhenIdle);
            peekInterval = settings.Get<TimeSpan>(WellKnownConfigurationKeys.ReceiverPeekInterval);
            messageInvisibleTime = settings.Get<TimeSpan>(WellKnownConfigurationKeys.ReceiverMessageInvisibleTime);

            var nativeDelayedDeliveryIsEnabled = NativeDelayedDeliveryIsEnabled();

            // This call mutates settings holder and should not be invoked more than once. This value is used for Diagnostics Section upon startup as well.
            var delayedDeliveryTableName = GetDelayedDeliveryTableName(settings);

            nativeDelayedDelivery = new NativeDelayDelivery(
                cloudTableClientProvider,
                blobServiceClientProvider,
                delayedDeliveryTableName,
                nativeDelayedDeliveryIsEnabled,
                settings.ErrorQueueAddress(),
                GetRequiredTransactionMode(),
                maximumWaitTime,
                peekInterval,
                BuildDispatcher);

            addressGenerator = new QueueAddressGenerator(settings.GetOrDefault<Func<string, string>>(WellKnownConfigurationKeys.QueueSanitizer));

            object delayedDelivery;
            if (nativeDelayedDeliveryIsEnabled)
            {
                delayedDelivery = new
                {
                    NativeDelayedDeliveryIsEnabled = true,
                    NativeDelayedDeliveryTableName = delayedDeliveryTableName,
                    TimeoutManagerEnabled = this.settings.Get<bool>(WellKnownConfigurationKeys.DelayedDelivery.EnableTimeoutManager)
                };
            }
            else
            {
                delayedDelivery = new
                {
                    NativeDelayedDeliveryIsEnabled = false,
                    TimeoutManagerEnabled = this.settings.Get<bool>(WellKnownConfigurationKeys.DelayedDelivery.EnableTimeoutManager)
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
        public override OutboundRoutingPolicy OutboundRoutingPolicy { get; } = new OutboundRoutingPolicy(OutboundRoutingType.Unicast, OutboundRoutingType.Unicast, OutboundRoutingType.Unicast);

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

        public override TransportReceiveInfrastructure ConfigureReceiveInfrastructure()
        {
            Logger.Debug("Configuring receive infrastructure");

            if (!settings.TryGet<IProvideQueueServiceClient>(out var queueServiceClientProvider))
            {
                queueServiceClientProvider = new QueueServiceClientProvidedByConnectionString(connectionString);
            }

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
            if (!settings.TryGet<IProvideQueueServiceClient>(out var queueServiceClientProvider))
            {
                queueServiceClientProvider = new QueueServiceClientProvidedByConnectionString(connectionString);
            }

            var addressing = GetAddressing(settings, queueServiceClientProvider);
            return new Dispatcher(addressGenerator, addressing, serializer, nativeDelayedDelivery.ShouldDispatch);
        }

        AzureStorageAddressingSettings GetAddressing(ReadOnlySettings settings, IProvideQueueServiceClient queueServiceClientProvider)
        {
            var addressing = settings.GetOrDefault<AzureStorageAddressingSettings>() ?? new AzureStorageAddressingSettings();

            if (settings.TryGet<AccountConfigurations>(out var accounts) == false)
            {
                accounts = new AccountConfigurations();
            }

            const string defaultAccountAlias = "";

            addressing.SetAddressGenerator(addressGenerator);
            addressing.RegisterMapping(accounts.defaultAlias ?? defaultAccountAlias, accounts.mappings);
            addressing.Add(new AccountInfo(defaultAccountAlias, queueServiceClientProvider.Client), false);

            return addressing;
        }

        public override TransportSubscriptionInfrastructure ConfigureSubscriptionInfrastructure()
        {
            throw new NotSupportedException("Azure Storage Queue transport doesn't support native pub sub");
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

        public override Task Start()
        {
            return nativeDelayedDelivery.Start();
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

        readonly ReadOnlySettings settings;
        readonly string connectionString;
        readonly MessageWrapperSerializer serializer;
        NativeDelayDelivery nativeDelayedDelivery;
        QueueAddressGenerator addressGenerator;
        readonly TimeSpan maximumWaitTime;
        readonly TimeSpan peekInterval;
        readonly TimeSpan messageInvisibleTime;

        static readonly ILog Logger = LogManager.GetLogger<AzureStorageQueueInfrastructure>();
    }
}