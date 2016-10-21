namespace NServiceBus.AzureStorageQueues
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Transports.WindowsAzureStorageQueues.DelayDelivery;
    using Config;
    using DelayedDelivery;
    using Performance.TimeToBeReceived;
    using Routing;
    using Serialization;
    using Settings;
    using Transport;

    class AzureStorageQueueInfrastructure : TransportInfrastructure
    {
        internal AzureStorageQueueInfrastructure(ReadOnlySettings settings, string connectionString)
        {
            this.settings = settings;
            this.connectionString = connectionString;
            serializer = BuildSerializer(settings);

            timeoutsTableName = settings.GetOrDefault<string>(WellKnownConfigurationKeys.NativeTimeoutsTableName);

            var contraints = new List<Type>
            {
                typeof(DiscardIfNotReceivedBefore),
                typeof(NonDurableDelivery)
            };
            useNativeTimeouts = timeoutsTableName != null;
            if (useNativeTimeouts)
            {
                contraints.Add(typeof(DelayedDeliveryConstraint));
                delayedDelivery = new NativeDelayDelivery(connectionString, timeoutsTableName);
                shouldDispatch = delayedDelivery.ShouldDispatch;
            }
            else
            {
                var @true = Task.FromResult(true);
                shouldDispatch = u => @true;
            }
            DeliveryConstraints = contraints;
        }

        public override IEnumerable<Type> DeliveryConstraints { get; }

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
                    var addressGenerator = GetAddressGenerator(settings);
                    var maximumWaitTime = settings.Get<TimeSpan>(WellKnownConfigurationKeys.ReceiverMaximumWaitTimeWhenIdle);
                    var peekInterval = settings.Get<TimeSpan>(WellKnownConfigurationKeys.ReceiverPeekInterval);

                    var receiver = new AzureMessageQueueReceiver(unwrapper, client, addressGenerator, new BackoffStrategy(maximumWaitTime, peekInterval))
                    {
                        MessageInvisibleTime = settings.Get<TimeSpan>(WellKnownConfigurationKeys.ReceiverMessageInvisibleTime),

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
            object useAccountNames;

            AccountConfigurations accounts;
            if (settings.TryGet(out accounts) == false)
            {
                accounts = new AccountConfigurations();
            }

            var shouldUseAccountNames = settings.TryGet(WellKnownConfigurationKeys.UseAccountNamesInsteadOfConnectionStrings, out useAccountNames);

            addressing.RegisterMapping(accounts.defaultAlias, accounts.mappings, shouldUseAccountNames);
            addressing.Add(QueueAddress.DefaultStorageAccountAlias, connectionString, false);

            return addressing;
        }

        static QueueAddressGenerator GetAddressGenerator(ReadOnlySettings settings)
        {
            return new QueueAddressGenerator(settings);
        }

        static MessageWrapperSerializer BuildSerializer(ReadOnlySettings settings)
        {
            SerializationDefinition wrapperSerializer;
            if (settings.TryGet(WellKnownConfigurationKeys.MessageWrapperSerializationDefinition, out wrapperSerializer))
            {
                return new MessageWrapperSerializer(wrapperSerializer.Configure(settings)(MessageWrapperSerializer.GetMapper()));
            }

            return new MessageWrapperSerializer(AzureStorageQueueTransport.GetMainSerializer(MessageWrapperSerializer.GetMapper(), settings));
        }

        public override TransportSendInfrastructure ConfigureSendInfrastructure()
        {
            return new TransportSendInfrastructure(BuildDispatcher, () => Task.FromResult(StartupCheckResult.Success));
        }

        Dispatcher BuildDispatcher()
        {
            var addressing = GetAddressing(settings, connectionString);
            var addressRetriever = GetAddressGenerator(settings);
            return new Dispatcher(addressRetriever, addressing, serializer, shouldDispatch);
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
                queue.Append("." + logicalAddress.Qualifier);
            }

            return queue.ToString();
        }

        public override async Task Start()
        {
            if (useNativeTimeouts)
            {
                var maximumWaitTime = settings.Get<TimeSpan>(WellKnownConfigurationKeys.ReceiverMaximumWaitTimeWhenIdle);
                var peekInterval = settings.Get<TimeSpan>(WellKnownConfigurationKeys.ReceiverPeekInterval);
                poller = new TimeoutsPoller(connectionString, BuildDispatcher(), timeoutsTableName, new BackoffStrategy(maximumWaitTime, peekInterval));
                cancellationSource = new CancellationTokenSource();
                await poller.Start(settings, cancellationSource.Token).ConfigureAwait(false);
                await delayedDelivery.Init().ConfigureAwait(false);
            }
        }

        public override Task Stop()
        {
            cancellationSource.Cancel();
            return poller != null ? poller.Stop() : TaskEx.CompletedTask;
        }

        readonly ReadOnlySettings settings;
        readonly string connectionString;
        readonly MessageWrapperSerializer serializer;
        readonly string timeoutsTableName;
        NativeDelayDelivery delayedDelivery;
        bool useNativeTimeouts;
        readonly Func<UnicastTransportOperation, Task<bool>> shouldDispatch;
        TimeoutsPoller poller;
        CancellationTokenSource cancellationSource;
    }
}