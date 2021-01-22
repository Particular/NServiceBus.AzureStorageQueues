using Azure.Storage.Queues.Models;
using NServiceBus.Azure.Transports.WindowsAzureStorageQueues;

namespace NServiceBus.Transport.AzureStorageQueues
{
    using System;
    using System.Threading.Tasks;
    using Logging;
    using Routing;
    using Serialization;
    using Transport;

    class AzureStorageQueueInfrastructure : TransportInfrastructure
    {
        internal AzureStorageQueueInfrastructure(HostSettings hostSettings,
            TransportTransactionMode transportTransactionMode,
            TimeSpan messageInvisibleTime,
            TimeSpan peekInterval,
            TimeSpan maximumWaitTimeWhenIdle,
            NativeDelayDeliveryPersistence nativeDelayDeliveryPersistence,
            NativeDelayedDeliveryProcessor nativeDelayedDeliveryProcessor,
            int? receiverBatchSize,
            int? degreeOfReceiveParallelism,
            QueueAddressGenerator addressGenerator,
            NativeDelayedDeliverySettings delayedDeliverySettings,
            IQueueServiceClientProvider queueServiceClientProvider,
            IBlobServiceClientProvider blobServiceClientProvider,
            ICloudTableClientProvider cloudTableClientProvider,
            SerializationDefinition messageWrapperSerializationDefinition,
            Func<QueueMessage, MessageWrapper> messageUnwrapper,
            ReceiveSettings[] receiveSettings,
            Dispatcher dispatcher,
            AzureStorageAddressingSettings azureStorageAddressing)
        {
            this.messageInvisibleTime = messageInvisibleTime;
            this.peekInterval = peekInterval;
            this.maximumWaitTimeWhenIdle = maximumWaitTimeWhenIdle;
            this.nativeDelayDeliveryPersistence = nativeDelayDeliveryPersistence;
            this.nativeDelayedDeliveryProcessor = nativeDelayedDeliveryProcessor;
            this.receiverBatchSize = receiverBatchSize;
            this.degreeOfReceiveParallelism = degreeOfReceiveParallelism;
            this.addressGenerator = addressGenerator;
            this.queueServiceClientProvider = queueServiceClientProvider;
            this.messageWrapperSerializationDefinition = messageWrapperSerializationDefinition;
            this.messageUnwrapper = messageUnwrapper;
            this.receiveSettings = receiveSettings;
            this.azureStorageAddressing = azureStorageAddressing;

            //TODO: Move to TransportDef.Initialize?
            //object delayedDeliveryDiagnosticSection;
            // if (enableNativeDelayedDelivery)
            // {
            //     delayedDeliveryDiagnosticSection = new
            //     {
            //         NativeDelayedDeliveryIsEnabled = true,
            //         NativeDelayedDeliveryTableName = delayedDeliveryTableName,
            //         UserDefinedNativeDelayedDeliveryTableName = userDefinedNativeDelayedDeliveryTableName
            //     };
            // }
            // else
            // {
            //     delayedDeliveryDiagnosticSection = new
            //     {
            //         NativeDelayedDeliveryIsEnabled = false,
            //     };
            // }

            // hostSettings.StartupDiagnostic.Add("NServiceBus.Transport.AzureStorageQueues", new
            // {
            //     ConnectionMechanism = new
            //     {
            //         Queue = queueServiceClientProvider is ConnectionStringQueueServiceClientProvider ? "ConnectionString" : "QueueServiceClient",
            //         Table = cloudTableClientProvider is ConnectionStringCloudTableClientProvider ? "ConnectionString" : "CloudTableClient",
            //         Blob = blobServiceClientProvider is ConnectionStringBlobServiceClientProvider ? "ConnectionString" : "BlobServiceClient",
            //     },
            //     MessageWrapperSerializer = this.messageWrapperSerializationDefinition == null ? "Default" : "Custom",
            //     MessageEnvelopeUnwrapper = this.messageUnwrapper == null ? "Default" : "Custom",
            //     DelayedDelivery = delayedDeliveryDiagnosticSection,
            //     TransactionMode = Enum.GetName(typeof(TransportTransactionMode), transportTransactionMode),
            //     ReceiverBatchSize = receiverBatchSize.HasValue ? receiverBatchSize.Value.ToString(CultureInfo.InvariantCulture) : "Default",
            //     DegreeOfReceiveParallelism = degreeOfReceiveParallelism.HasValue ? degreeOfReceiveParallelism.Value.ToString(CultureInfo.InvariantCulture) : "Default",
            //     MaximumWaitTimeWhenIdle = this.maximumWaitTimeWhenIdle,
            //     PeekInterval = peekInterval,
            //     MessageInvisibleTime = messageInvisibleTime
            // });
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

        public override TransportSubscriptionInfrastructure ConfigureSubscriptionInfrastructure()
        {
            throw new NotSupportedException("Azure Storage Queue transport doesn't support native pub sub");
        }

        public override EndpointInstance BindToLocalEndpoint(EndpointInstance instance)
        {
            return instance;
        }

        public override Task DisposeAsync()
        {
            return nativeDelayedDeliveryProcessor.Stop();
        }

        readonly QueueAddressGenerator addressGenerator;
        private readonly IQueueServiceClientProvider queueServiceClientProvider;
        private readonly SerializationDefinition messageWrapperSerializationDefinition;
        private readonly Func<QueueMessage, MessageWrapper> messageUnwrapper;
        private readonly ReceiveSettings[] receiveSettings;
        readonly TimeSpan maximumWaitTimeWhenIdle;
        private NativeDelayDeliveryPersistence nativeDelayDeliveryPersistence;
        private readonly NativeDelayedDeliveryProcessor nativeDelayedDeliveryProcessor;
        private readonly int? receiverBatchSize;
        private readonly int? degreeOfReceiveParallelism;
        readonly TimeSpan peekInterval;
        readonly TimeSpan messageInvisibleTime;
        private AzureStorageAddressingSettings azureStorageAddressing;

        static readonly ILog Logger = LogManager.GetLogger<AzureStorageQueueInfrastructure>();
    }
}
