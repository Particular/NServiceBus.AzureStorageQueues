namespace NServiceBus.Transport.AzureStorageQueues
{
    using System.Collections.ObjectModel;
    using System.Threading.Tasks;
    using Logging;
    using Transport;

    class AzureStorageQueueInfrastructure : TransportInfrastructure
    {
        internal AzureStorageQueueInfrastructure(Dispatcher dispatcher, ReadOnlyCollection<IMessageReceiver> receivers, NativeDelayedDeliveryProcessor nativeDelayedDeliveryProcessor)
        {
            Dispatcher = dispatcher;
            Receivers = receivers;
            this.nativeDelayedDeliveryProcessor = nativeDelayedDeliveryProcessor;

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

        public override Task DisposeAsync()
        {
            return nativeDelayedDeliveryProcessor.Stop();
        }

        private readonly NativeDelayedDeliveryProcessor nativeDelayedDeliveryProcessor;

        static readonly ILog Logger = LogManager.GetLogger<AzureStorageQueueInfrastructure>();
    }
}
