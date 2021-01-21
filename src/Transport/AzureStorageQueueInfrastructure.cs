using System.Collections.Immutable;
using System.Reflection;
using Azure.Storage.Queues.Models;
using NServiceBus.Azure.Transports.WindowsAzureStorageQueues;
using NServiceBus.MessageInterfaces;

namespace NServiceBus.Transport.AzureStorageQueues
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
        internal AzureStorageQueueInfrastructure(HostSettings hostSettings,
            TransportTransactionMode transportTransactionMode,
            TimeSpan messageInvisibleTime,
            TimeSpan peekInterval,
            TimeSpan maximumWaitTimeWhenIdle,
            bool enableNativeDelayedDelivery,
            int? receiverBatchSize,
            int? degreeOfReceiveParallelism,
            QueueAddressGenerator addressGenerator,
            string delayedDeliveryTableName,
            IQueueServiceClientProvider queueServiceClientProvider,
            IBlobServiceClientProvider blobServiceClientProvider,
            ICloudTableClientProvider cloudTableClientProvider,
            SerializationDefinition messageWrapperSerializationDefinition,
            Func<QueueMessage, MessageWrapper> messageUnwrapper,
            ReceiveSettings[] receiveSettings,
            AzureStorageAddressingSettings azureStorageAddressing)
        {
            this.messageInvisibleTime = messageInvisibleTime;
            this.peekInterval = peekInterval;
            this.maximumWaitTimeWhenIdle = maximumWaitTimeWhenIdle;
            this.receiverBatchSize = receiverBatchSize;
            this.degreeOfReceiveParallelism = degreeOfReceiveParallelism;
            this.addressGenerator = addressGenerator;
            this.queueServiceClientProvider = queueServiceClientProvider;
            this.messageWrapperSerializationDefinition = messageWrapperSerializationDefinition;
            this.messageUnwrapper = messageUnwrapper;
            this.receiveSettings = receiveSettings;

            var userDefinedNativeDelayedDeliveryTableName = true;
            if (enableNativeDelayedDelivery)
            {
                if (string.IsNullOrEmpty(delayedDeliveryTableName))
                {
                    delayedDeliveryTableName = GenerateDelayedDeliveryTableName(hostSettings.Name);
                    userDefinedNativeDelayedDeliveryTableName = false;
                }

                nativeDelayedDelivery = new NativeDelayDelivery(
                    cloudTableClientProvider,
                    blobServiceClientProvider,
                    delayedDeliveryTableName,
                    receiveSettings.ToImmutableDictionary(settings => settings.ReceiveAddress, settings => settings.ErrorQueue),
                    transportTransactionMode,
                    this.maximumWaitTimeWhenIdle,
                    peekInterval,
                    BuildDispatcher);

                supportedDeliveryConstraints.Add(typeof(DelayDeliveryWith));
                supportedDeliveryConstraints.Add(typeof(DoNotDeliverBefore));
            }

            object delayedDeliveryDiagnosticSection;
            if (enableNativeDelayedDelivery)
            {
                delayedDeliveryDiagnosticSection = new
                {
                    NativeDelayedDeliveryIsEnabled = true,
                    NativeDelayedDeliveryTableName = delayedDeliveryTableName,
                    UserDefinedNativeDelayedDeliveryTableName = userDefinedNativeDelayedDeliveryTableName
                };
            }
            else
            {
                delayedDeliveryDiagnosticSection = new
                {
                    NativeDelayedDeliveryIsEnabled = false,
                };
            }

            hostSettings.StartupDiagnostic.Add("NServiceBus.Transport.AzureStorageQueues", new
            {
                ConnectionMechanism = new
                {
                    Queue = queueServiceClientProvider is ConnectionStringQueueServiceClientProvider ? "ConnectionString" : "QueueServiceClient",
                    Table = cloudTableClientProvider is ConnectionStringCloudTableClientProvider ? "ConnectionString" : "CloudTableClient",
                    Blob = blobServiceClientProvider is ConnectionStringBlobServiceClientProvider ? "ConnectionString" : "BlobServiceClient",
                },
                MessageWrapperSerializer = this.messageWrapperSerializationDefinition == null ? "Default" : "Custom",
                MessageEnvelopeUnwrapper = this.messageUnwrapper == null ? "Default" : "Custom",
                DelayedDelivery = delayedDeliveryDiagnosticSection,
                TransactionMode = Enum.GetName(typeof(TransportTransactionMode), transportTransactionMode),
                ReceiverBatchSize = receiverBatchSize.HasValue ? receiverBatchSize.Value.ToString(CultureInfo.InvariantCulture) : "Default",
                DegreeOfReceiveParallelism = degreeOfReceiveParallelism.HasValue ? degreeOfReceiveParallelism.Value.ToString(CultureInfo.InvariantCulture) : "Default",
                MaximumWaitTimeWhenIdle = this.maximumWaitTimeWhenIdle,
                PeekInterval = peekInterval,
                MessageInvisibleTime = messageInvisibleTime
            });
        }

        public override Task ValidateNServiceBusSettings(ReadOnlySettings settings)
        {
            serializer = BuildSerializer(messageWrapperSerializationDefinition, settings);
            return base.ValidateNServiceBusSettings(settings);
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

        public override OutboundRoutingPolicy OutboundRoutingPolicy { get; } = new OutboundRoutingPolicy(OutboundRoutingType.Unicast, OutboundRoutingType.Unicast, OutboundRoutingType.Unicast);

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
                    var unwrapper = messageUnwrapper != null
                        ? (IMessageEnvelopeUnwrapper)new UserProvidedEnvelopeUnwrapper(messageUnwrapper)
                        : new DefaultMessageEnvelopeUnwrapper(serializer);

                    var receiver = new AzureMessageQueueReceiver(unwrapper, queueServiceClientProvider, addressGenerator)
                    {
                        MessageInvisibleTime = messageInvisibleTime,
                    };

                    return new MessagePump(receiver, degreeOfReceiveParallelism, receiverBatchSize, maximumWaitTimeWhenIdle, peekInterval);
                },
                () => new AzureMessageQueueCreator(queueServiceClientProvider, addressGenerator),
                () => Task.FromResult(StartupCheckResult.Success)
            );
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
            //TODO: Should this be started for SendOnly endpoints?
            if (nativeDelayedDelivery != null)
            {
                return nativeDelayedDelivery.Start();
            }

            return Task.CompletedTask;
        }

        public override Task Stop()
        {
            //TODO: Should this be stopped for SendOnly endpoints?
            if (nativeDelayedDelivery != null)
            {
                return nativeDelayedDelivery.Stop();
            }

            return Task.CompletedTask;
        }

        internal const string SerializerSettingsKey = "MainSerializer";
        MessageWrapperSerializer serializer;
        readonly List<Type> supportedDeliveryConstraints = new List<Type> { typeof(DiscardIfNotReceivedBefore) };
        readonly NativeDelayDelivery nativeDelayedDelivery;
        readonly QueueAddressGenerator addressGenerator;
        private readonly IQueueServiceClientProvider queueServiceClientProvider;
        private readonly SerializationDefinition messageWrapperSerializationDefinition;
        private readonly Func<QueueMessage, MessageWrapper> messageUnwrapper;
        private readonly ReceiveSettings[] receiveSettings;
        readonly TimeSpan maximumWaitTimeWhenIdle;
        private readonly int? receiverBatchSize;
        private readonly int? degreeOfReceiveParallelism;
        readonly TimeSpan peekInterval;

        readonly TimeSpan messageInvisibleTime;

        static readonly ILog Logger = LogManager.GetLogger<AzureStorageQueueInfrastructure>();
    }
}
