namespace NServiceBus
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Threading.Tasks;
    using NServiceBus.Azure.Transports.WindowsAzureStorageQueues;
    using NServiceBus.Azure.Transports.WindowsAzureStorageQueues.Config;
    using NServiceBus.Performance.TimeToBeReceived;
    using NServiceBus.Routing;
    using NServiceBus.Serialization;
    using NServiceBus.Settings;
    using NServiceBus.Transports;

    /// <summary>
    ///     Transport definition for AzureStorageQueue
    /// </summary>
    public class AzureStorageQueueTransport : TransportDefinition
    {
        private MessageWrapperSerializer serializer;

        public override string ExampleConnectionStringForErrorMessage { get; } =
            "DefaultEndpointsProtocol=[http|https];AccountName=myAccountName;AccountKey=myAccountKey";

        protected override TransportReceivingConfigurationResult ConfigureForReceiving(TransportReceivingConfigurationContext context)
        {
            var settings = context.Settings;
            var connectionString = new ConnectionString(context.ConnectionString);
            var client = new CreateQueueClients().CreateRecevier(connectionString);

            return new TransportReceivingConfigurationResult(
                () =>
                {
                    var addressing = GetAddressing(settings, connectionString.Value);
                    var receiver = new AzureMessageQueueReceiver(GetSerializer(settings), client, GetAddressGenerator(settings))
                    {
                        PurgeOnStartup = settings.Get<bool>(WellKnownConfigurationKeys.PurgeOnStartup),
                        MaximumWaitTimeWhenIdle = settings.Get<int>(WellKnownConfigurationKeys.ReceiverMaximumWaitTimeWhenIdle),
                        MessageInvisibleTime = settings.Get<int>(WellKnownConfigurationKeys.ReceiverMessageInvisibleTime),
                        PeekInterval = settings.Get<int>(WellKnownConfigurationKeys.ReceiverPeekInterval),
                        BatchSize = settings.Get<int>(WellKnownConfigurationKeys.ReceiverBatchSize)
                    };

                    return new MessagePump(receiver, addressing);
                },
                () => new AzureMessageQueueCreator(client, GetAddressGenerator(settings), settings.GetOrDefault<bool>(WellKnownConfigurationKeys.TransportCreateSendingQueues)),
                () => Task.FromResult(StartupCheckResult.Success));
        }

        private static QueueAddressGenerator GetAddressGenerator(ReadOnlySettings settings)
        {
            return new QueueAddressGenerator(settings);
        }

        protected override TransportSendingConfigurationResult ConfigureForSending(TransportSendingConfigurationContext context)
        {
            var settings = context.Settings;
            var connectionString = context.ConnectionString;
            return new TransportSendingConfigurationResult(
                () =>
                {
                    var addressing = GetAddressing(settings, connectionString);

                    var queueCreator = new CreateQueueClients();
                    var addressRetriever = GetAddressGenerator(settings);
                    return new Dispatcher(queueCreator, GetSerializer(settings), addressRetriever, addressing);
                },
                () => Task.FromResult(StartupCheckResult.Success));
        }

        private static AzureStorageAddressingSettings GetAddressing(ReadOnlySettings settings, string connectionString)
        {
            var addressing = settings.GetOrDefault<AzureStorageAddressingSettings>() ?? new AzureStorageAddressingSettings();

            addressing.Add(QueueAddress.DefaultStorageAccountName, connectionString, false);

            return addressing;
        }

        public override IEnumerable<Type> GetSupportedDeliveryConstraints()
        {
            return new[]
            {
                typeof(DiscardIfNotReceivedBefore),
                typeof(NonDurableDelivery)
            };
        }

        public override TransportTransactionMode GetSupportedTransactionMode()
        {
            return TransportTransactionMode.ReceiveOnly;
        }

        public override IManageSubscriptions GetSubscriptionManager()
        {
            throw new NotSupportedException("Azure Storage Queue transport doesn't support native pub sub");
        }

        public override EndpointInstance BindToLocalEndpoint(EndpointInstance instance, ReadOnlySettings settings)
        {
            return instance;
        }

        public override string ToTransportAddress(LogicalAddress logicalAddress)
        {
            return logicalAddress.ToString();
        }

        public override OutboundRoutingPolicy GetOutboundRoutingPolicy(ReadOnlySettings settings)
        {
            // Azure Storage Queues does not support mulitcast, hence all the messages are sent with Unicast
            return new OutboundRoutingPolicy(OutboundRoutingType.Unicast, OutboundRoutingType.Unicast, OutboundRoutingType.Unicast);
        }

        MessageWrapperSerializer GetSerializer(ReadOnlySettings settings)
        {
            if (serializer != null)
            {
                return serializer;
            }

            serializer = BuildSerializer(settings);
            return serializer;
        }

        static MessageWrapperSerializer BuildSerializer(ReadOnlySettings settings)
        {
            MessageWrapperSerializer serializer;
            if (settings.TryGet(WellKnownConfigurationKeys.MessageWrapperSerializer, out serializer) == false)
            {
                serializer = MessageWrapperSerializer.TryBuild(settings.GetOrDefault<SerializationDefinition>(),
                    settings.GetOrDefault<Func<SerializationDefinition, MessageWrapperSerializer>>(WellKnownConfigurationKeys.MessageWrapperSerializerFactory));
                if (serializer == null)
                {
                    throw new ConfigurationErrorsException($"The bus is configured using different {typeof(SerializationDefinition).Name} than defaults provided by the NServiceBus. " +
                                                           $"Register a custom serialization with {typeof(AzureStorageTransportExtensions).Name}.SerializeMessageWrapperWith()");
                }
            }
            return serializer;
        }
    }
}