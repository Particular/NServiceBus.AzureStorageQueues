namespace NServiceBus
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Queue;
    using NServiceBus.Azure.Transports.WindowsAzureStorageQueues;
    using NServiceBus.Config;
    using NServiceBus.MessageInterfaces.MessageMapper.Reflection;
    using NServiceBus.Performance.TimeToBeReceived;
    using NServiceBus.Routing;
    using NServiceBus.Serialization;
    using NServiceBus.Serializers.Json;
    using NServiceBus.Settings;
    using NServiceBus.Transports;

    /// <summary>
    ///     Transport definition for AzureStorageQueue
    /// </summary>
    public class AzureStorageQueueTransport : TransportDefinition
    {
        readonly IMessageSerializer Serializer;

        public AzureStorageQueueTransport()
        {
            var mapper = new MessageMapper();
            mapper.Initialize(new[]
            {
                typeof(MessageWrapper)
            });
            Serializer = new JsonMessageSerializer(mapper);
        }

        public override string ExampleConnectionStringForErrorMessage { get; } =
            "DefaultEndpointsProtocol=[http|https];AccountName=myAccountName;AccountKey=myAccountKey";

        protected override TransportReceivingConfigurationResult ConfigureForReceiving(TransportReceivingConfigurationContext context)
        {
            var client = BuildClient(context.Settings, context.ConnectionString);
            var configSection = context.Settings.GetConfigSection<AzureQueueConfig>();

            return new TransportReceivingConfigurationResult(
                () =>
                {
                    var receiver = new AzureMessageQueueReceiver(Serializer, client);
                    if (configSection != null)
                    {
                        receiver.PurgeOnStartup = configSection.PurgeOnStartup;
                        receiver.MaximumWaitTimeWhenIdle = configSection.MaximumWaitTimeWhenIdle;
                        receiver.MessageInvisibleTime = configSection.MessageInvisibleTime;
                        receiver.PeekInterval = configSection.PeekInterval;
                        receiver.BatchSize = configSection.BatchSize;
                    }

                    return new PollingDequeueStrategy(receiver);
                },
                () => new AzureMessageQueueCreator(client),
                () => Task.FromResult(StartupCheckResult.Success));
        }

        static CloudQueueClient BuildClient(ReadOnlySettings settings, string connectionStringFromContext)
        {
            CloudQueueClient queueClient;

            var configSection = settings.GetConfigSection<AzureQueueConfig>();

            var connectionString = TryGetConnectionString(configSection, connectionStringFromContext);

            if (string.IsNullOrEmpty(connectionString))
            {
                queueClient = CloudStorageAccount.DevelopmentStorageAccount.CreateCloudQueueClient();
            }
            else
            {
                queueClient = CloudStorageAccount.Parse(connectionString).CreateCloudQueueClient();
            }

            return queueClient;
        }

        static string TryGetConnectionString(AzureQueueConfig configSection, string defaultConnectionString)
        {
            var connectionString = defaultConnectionString;

            if (string.IsNullOrEmpty(connectionString))
            {
                if (configSection != null)
                {
                    connectionString = configSection.ConnectionString;
                }
            }

            return connectionString;
        }

        protected override TransportSendingConfigurationResult ConfigureForSending(TransportSendingConfigurationContext context)
        {
            var settings = context.Settings;
            var connectionString = context.ConnectionString;
            return new TransportSendingConfigurationResult(
                () => new AzureMessageQueueSender(new CreateQueueClients(settings, connectionString), Serializer, settings, connectionString),
                () => Task.FromResult(StartupCheckResult.Success));
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
            return TransportTransactionMode.None;
        }

        public override IManageSubscriptions GetSubscriptionManager()
        {
            throw new NotSupportedException("Azure Storage Queue transport doesn't support native pub sub");
        }

        public override EndpointInstance BindToLocalEndpoint(EndpointInstance instance, ReadOnlySettings settings)
        {
            var endpointBoundToLocalEndpoint = new EndpointInstance(instance.Endpoint, null, instance.Properties);
            return endpointBoundToLocalEndpoint;
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
    }
}