﻿namespace NServiceBus.Transport.AzureStorageQueues
{
    using System;
    using System.Globalization;
    using System.Collections.Generic;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading.Tasks;
    using DelayedDelivery;
    using Logging;
    using Performance.TimeToBeReceived;
    using Routing;
    using Serialization;
    using Settings;
    using Transport;

    class AzureStorageQueueInfrastructure : TransportInfrastructure
    {
        internal AzureStorageQueueInfrastructure(TimeSpan messageInvisibleTime,
            TimeSpan peekInterval,
            TimeSpan maximumWaitTimeWhenIdle,
            bool enableNativeDelayedDelivery,
            int? receiverBatchSize,
            QueueAddressGenerator addressGenerator,
            IQueueServiceClientProvider queueServiceClientProvider,
            IBlobServiceClientProvider blobServiceClientProvider,
            ICloudTableClientProvider cloudTableClientProvider)
        {
            this.messageInvisibleTime = messageInvisibleTime;
            this.peekInterval = peekInterval;
            this.maximumWaitTimeWhenIdle = maximumWaitTimeWhenIdle;
            this.receiverBatchSize = receiverBatchSize;
            this.addressGenerator = addressGenerator;
            this.queueServiceClientProvider = queueServiceClientProvider;

            serializer = BuildSerializer(settings, out var userProvidedSerializer);

            string delayedDeliveryTableName = null;
            if (enableNativeDelayedDelivery)
            {
                // This call mutates settings holder and should not be invoked more than once. This value is used for Diagnostics Section upon startup as well.
                delayedDeliveryTableName = GetDelayedDeliveryTableName(settings);

                nativeDelayedDelivery = new NativeDelayDelivery(
                    cloudTableClientProvider,
                    blobServiceClientProvider,
                    delayedDeliveryTableName,
                    settings.ErrorQueueAddress(),
                    GetRequiredTransactionMode(),
                    this.maximumWaitTimeWhenIdle,
                    peekInterval,
                    BuildDispatcher);

                supportedDeliveryConstraints.Add(typeof(DelayDeliveryWith));
                supportedDeliveryConstraints.Add(typeof(DoNotDeliverBefore));
            }

            object delayedDelivery;
            if (nativeDelayedDeliveryIsEnabled)
            {
                delayedDelivery = new
                {
                    NativeDelayedDeliveryIsEnabled = true,
                    NativeDelayedDeliveryTableName = delayedDeliveryTableName
                };
            }
            else
            {
                delayedDelivery = new
                {
                    NativeDelayedDeliveryIsEnabled = false,
                };
            }

            settings.AddStartupDiagnosticsSection("NServiceBus.Transport.AzureStorageQueues", new
            {
                ConnectionMechanism = new
                {
                    Queue = settings.TryGet<IQueueServiceClientProvider>(out _) ? "QueueServiceClient" : "ConnectionString",
                    Table = settings.TryGet<ICloudTableClientProvider>(out _) ? "CloudTableClient" : "ConnectionString",
                    Blob = settings.TryGet<IBlobServiceClientProvider>(out _) ? "BlobServiceClient" : "ConnectionString",
                },
                MessageWrapperSerializer = userProvidedSerializer ? "Custom" : "Default",
                MessageEnvelopeUnwrapper = settings.HasExplicitValue<IMessageEnvelopeUnwrapper>() ? "Custom" : "Default",
                DelayedDelivery = delayedDelivery,
                TransactionMode = Enum.GetName(typeof(TransportTransactionMode), GetRequiredTransactionMode()),
                ReceiverBatchSize = receiverBatchSize.HasValue ? receiverBatchSize.Value.ToString(CultureInfo.InvariantCulture) : "Default",
                DegreeOfReceiveParallelism = settings.TryGet(WellKnownConfigurationKeys.DegreeOfReceiveParallelism, out int degreeOfReceiveParallelism) ? degreeOfReceiveParallelism.ToString(CultureInfo.InvariantCulture) : "Default",
                MaximumWaitTimeWhenIdle = this.maximumWaitTimeWhenIdle,
                PeekInterval = peekInterval,
                MessageInvisibleTime = messageInvisibleTime
            });
        }

        public override IEnumerable<Type> DeliveryConstraints => supportedDeliveryConstraints;
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

        public override TransportReceiveInfrastructure ConfigureReceiveInfrastructure()
        {
            Logger.Debug("Configuring receive infrastructure");

            if (!settings.TryGet<IQueueServiceClientProvider>(out var queueServiceClientProvider))
            {
                queueServiceClientProvider = new ConnectionStringQueueServiceClientProvider(connectionString);
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

                    return new MessagePump(receiver, degreeOfReceiveParallelism, receiverBatchSize, maximumWaitTimeWhenIdle, peekInterval);
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
            return new TransportSendInfrastructure(BuildDispatcher, () => Task.FromResult(StartupCheckResult.Success));
        }

        Dispatcher BuildDispatcher()
        {
            var addressing = GetAddressing(settings, queueServiceClientProvider);
            return new Dispatcher(addressGenerator, addressing, serializer, nativeDelayedDelivery);
        }

        AzureStorageAddressingSettings GetAddressing(ReadOnlySettings settings, IQueueServiceClientProvider queueServiceClientProviderProvider)
        {
            var addressing = settings.GetOrDefault<AzureStorageAddressingSettings>() ?? new AzureStorageAddressingSettings();

            if (settings.TryGet<AccountConfigurations>(out var accounts) == false)
            {
                accounts = new AccountConfigurations();
            }

            const string defaultAccountAlias = "";

            addressing.SetAddressGenerator(addressGenerator);
            addressing.RegisterMapping(accounts.defaultAlias ?? defaultAccountAlias, accounts.mappings);
            addressing.Add(new AccountInfo(defaultAccountAlias, queueServiceClientProviderProvider.Client), false);

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

        public override Task Start()
        {
            if (nativeDelayedDelivery != null)
            {
                return nativeDelayedDelivery.Start();
            }

            return Task.CompletedTask;
        }

        public override Task Stop()
        {
            if (nativeDelayedDelivery != null)
            {
                return nativeDelayedDelivery.Stop();
            }

            return Task.CompletedTask;
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

        readonly MessageWrapperSerializer serializer;
        readonly List<Type> supportedDeliveryConstraints = new List<Type> { typeof(DiscardIfNotReceivedBefore) };
        readonly NativeDelayDelivery nativeDelayedDelivery;
        readonly QueueAddressGenerator addressGenerator;
        private readonly IQueueServiceClientProvider queueServiceClientProvider;
        readonly TimeSpan maximumWaitTimeWhenIdle;
        private readonly int? receiverBatchSize;
        readonly TimeSpan peekInterval;

        readonly TimeSpan messageInvisibleTime;

        static readonly ILog Logger = LogManager.GetLogger<AzureStorageQueueInfrastructure>();
    }
}
