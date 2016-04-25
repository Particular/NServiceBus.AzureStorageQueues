namespace NServiceBus.AzureStorageQueues
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Threading.Tasks;
    using Config;
    using Transports;
    using Performance.TimeToBeReceived;
    using Routing;
    using Serialization;
    using Settings;

    class AzureStorageQueueInfrastructure : TransportInfrastructure
    {
        internal AzureStorageQueueInfrastructure(ReadOnlySettings settings, string connectionString)
        {
            this.settings = settings;
            this.connectionString = connectionString;
            serializer = BuildSerializer(settings);
        }

        public override IEnumerable<Type> DeliveryConstraints { get; } = new[]
        {
            typeof(DiscardIfNotReceivedBefore),
            typeof(NonDurableDelivery)
        };

        public override TransportTransactionMode TransactionMode { get; } = TransportTransactionMode.ReceiveOnly;
        public override OutboundRoutingPolicy OutboundRoutingPolicy { get; } = new OutboundRoutingPolicy(OutboundRoutingType.Unicast, OutboundRoutingType.Unicast, OutboundRoutingType.Unicast);

        public override TransportReceiveInfrastructure ConfigureReceiveInfrastructure()
        {
            var connectionObject = new ConnectionString(connectionString);
            var client = CreateQueueClients.CreateReceiver(connectionObject);

            return new TransportReceiveInfrastructure(
                () =>
                {
                    var addressing = GetAddressing(settings, connectionString);
                    var unwrapper = new MessageEnvelopeUnwrapper(serializer);
                    var receiver = new AzureMessageQueueReceiver(unwrapper, client, GetAddressGenerator(settings))
                    {
                        MaximumWaitTimeWhenIdle = settings.Get<int>(WellKnownConfigurationKeys.ReceiverMaximumWaitTimeWhenIdle),
                        MessageInvisibleTime = settings.Get<int>(WellKnownConfigurationKeys.ReceiverMessageInvisibleTime),
                        PeekInterval = settings.Get<int>(WellKnownConfigurationKeys.ReceiverPeekInterval),
                        BatchSize = settings.Get<int>(WellKnownConfigurationKeys.ReceiverBatchSize)
                    };

                    int? degreeOfReceiveParallelism = null;
                    int parallelism;
                    if (settings.TryGet(WellKnownConfigurationKeys.DegreeOfReceiveParallelism, out parallelism))
                    {
                        degreeOfReceiveParallelism = parallelism;
                    }
                    return new MessagePump(receiver, addressing, degreeOfReceiveParallelism);
                },
                () => new AzureMessageQueueCreator(client, GetAddressGenerator(settings)),
                () => Task.FromResult(StartupCheckResult.Success)
                );
        }

        static AzureStorageAddressingSettings GetAddressing(ReadOnlySettings settings, string connectionString)
        {
            var addressing = settings.GetOrDefault<AzureStorageAddressingSettings>() ?? new AzureStorageAddressingSettings();

            addressing.Add(QueueAddress.DefaultStorageAccountName, connectionString, false);

            return addressing;
        }

        static QueueAddressGenerator GetAddressGenerator(ReadOnlySettings settings)
        {
            return new QueueAddressGenerator(settings);
        }

        static MessageWrapperSerializer BuildSerializer(ReadOnlySettings settings)
        {
            var definition = settings.GetOrDefault<SerializationDefinition>(WellKnownConfigurationKeys.MessageWrapperSerializationDefinition);
            if (definition == null)
            {
                definition = settings.GetOrDefault<SerializationDefinition>();
            }

            if (definition == null)
            {
                var name = typeof(SerializationDefinition).Name;
                throw new ConfigurationErrorsException($"There's no {name} configured either for the Azure Storage Queue transport or the bus itself. " +
                                                       "Use either endpointConfiguration.UseSerialization() or .UseTransport<AzureStorageQueueTransport>().SerializeMessageWrapperWith() to provide one.");
            }

            return new MessageWrapperSerializer(definition.Configure(settings));
        }

        public override TransportSendInfrastructure ConfigureSendInfrastructure()
        {
            return new TransportSendInfrastructure(
                () =>
                {
                    var addressing = GetAddressing(settings, connectionString);

                    var queueCreator = new CreateQueueClients();
                    var addressRetriever = GetAddressGenerator(settings);
                    return new Dispatcher(queueCreator, serializer, addressRetriever, addressing);
                },
                () => Task.FromResult(StartupCheckResult.Success));
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
            return logicalAddress.ToString();
        }

        ReadOnlySettings settings;
        string connectionString;
        MessageWrapperSerializer serializer;
    }
}