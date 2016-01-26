namespace NServiceBus
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Queue;
    using NServiceBus.Azure.Transports.WindowsAzureStorageQueues;
    using NServiceBus.Config;
    using NServiceBus.Routing;
    using NServiceBus.Serialization;
    using NServiceBus.Settings;
    using NServiceBus.Transports;

    /// <summary>
    ///     Transport definition for AzureStorageQueue
    /// </summary>
    public class AzureStorageQueueTransport : TransportDefinition
    {
        //public AzureStorageQueueTransport()
        //{
        //    HasSupportForDistributedTransactions = false;
        //}

        ///// <summary>
        ///// Gives implementations access to the <see cref="T:NServiceBus.BusConfiguration"/> instance at configuration time.
        ///// </summary>
        //protected override void Configure(BusConfiguration config)
        //{
        //    config.EnableFeature<AzureStorageQueueTransportConfiguration>();
        //    config.EnableFeature<TimeoutManagerBasedDeferral>();

        //    var settings = config.GetSettings();

        //    settings.EnableFeatureByDefault<MessageDrivenSubscriptions>();
        //    settings.EnableFeatureByDefault<StorageDrivenPublishing>();
        //    settings.EnableFeatureByDefault<TimeoutManager>();

        //    settings.SetDefault("SelectedSerializer", new JsonSerializer());

        //    settings.SetDefault("ScaleOut.UseSingleBrokerQueue", true); // default to one queue for all instances
        //    config.GetSettings().SetDefault("EndpointInstanceDiscriminator", QueueIndividualizer.Discriminator);

        //}

        // TODO: serializer initialization
        private static readonly IMessageSerializer Serializer = default(IMessageSerializer);

        public override string ExampleConnectionStringForErrorMessage { get; } = "DefaultEndpointsProtocol=[http|https];AccountName=myAccountName;AccountKey=myAccountKey";

        protected override TransportReceivingConfigurationResult ConfigureForReceiving(TransportReceivingConfigurationContext context)
        {
            var client = BuildClient(context.Settings);
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

        static CloudQueueClient BuildClient(ReadOnlySettings settings)
        {
            CloudQueueClient queueClient;

            var configSection = settings.GetConfigSection<AzureQueueConfig>();

            // TODO: the default connection string should be passed
            var connectionString = TryGetConnectionString(configSection, null);

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

        //protected override void Configure(FeatureConfigurationContext context, string con)
        //{
        //    context.Settings.Get<Conventions>().AddSystemMessagesConventions(t => typeof(MessageWrapper).IsAssignableFrom(t));

        //    var receiverConfig = context.Container.ConfigureComponent<AzureMessageQueueReceiver>(DependencyLifecycle.InstancePerCall);
        //    context.Container.ConfigureComponent<CreateQueueClients>(DependencyLifecycle.SingleInstance);
        //    context.Container.ConfigureComponent<AzureMessageQueueSender>(DependencyLifecycle.InstancePerCall);
        //    context.Container.ConfigureComponent<PollingDequeueStrategy>(DependencyLifecycle.InstancePerCall);
        //    context.Container.ConfigureComponent<AzureMessageQueueCreator>(DependencyLifecycle.InstancePerCall);

        //    context.Settings.ApplyTo<AzureMessageQueueReceiver>((IComponentConfig)receiverConfig);
        //}

        protected override TransportSendingConfigurationResult ConfigureForSending(TransportSendingConfigurationContext context)
        {
            throw new NotImplementedException();
        }

        public override IEnumerable<Type> GetSupportedDeliveryConstraints()
        {
            throw new NotImplementedException();
        }

        public override TransportTransactionMode GetSupportedTransactionMode()
        {
            throw new NotImplementedException();
        }

        public override IManageSubscriptions GetSubscriptionManager()
        {
            throw new NotImplementedException();
        }

        public override EndpointInstance BindToLocalEndpoint(EndpointInstance instance, ReadOnlySettings settings)
        {
            throw new NotImplementedException();
        }

        public override string ToTransportAddress(LogicalAddress logicalAddress)
        {
            throw new NotImplementedException();
        }

        public override OutboundRoutingPolicy GetOutboundRoutingPolicy(ReadOnlySettings settings)
        {
            throw new NotImplementedException();
        }
    }
}