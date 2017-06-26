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
    using Features;
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
            serializer = BuildSerializer(settings);

            delayedDeliverySettings = settings.GetOrCreate<DelayedDeliverySettings>();

            var timeoutManagerFeatureDisabled = settings.GetOrDefault<FeatureState>(typeof(TimeoutManager).FullName) == FeatureState.Disabled;
            var sendOnlyEndpoint = settings.GetOrDefault<bool>("Endpoint.SendOnly");

            if (timeoutManagerFeatureDisabled || sendOnlyEndpoint)
            {
                // TM is automatically disabled to do not throw during check
                delayedDeliverySettings.DisableTimeoutManager();
            }

            string tableName;
            if (IsNativeDelayedDeliveryCongifured(out tableName))
            {
                delayedDelivery = new NativeDelayDelivery(connectionString, tableName);
            }
        }

        public override IEnumerable<Type> DeliveryConstraints
        {
            get
            {
                yield return typeof(DiscardIfNotReceivedBefore);
                yield return typeof(NonDurableDelivery);

                if (delayedDelivery != null)
                {
                    yield return typeof(DoNotDeliverBefore);
                    yield return typeof(DelayDeliveryWith);
                }
            }
        }

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

                    var unwrapper = settings.HasSetting<IMessageEnvelopeUnwrapper>() ?
                        settings.GetOrDefault<IMessageEnvelopeUnwrapper>() :
                        new DefaultMessageEnvelopeUnwrapper(serializer);

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
            return new TransportSendInfrastructure(BuildDispatcher, () => Task.FromResult(NativeDelayDelivery.CheckForInvalidSettings(settings)));
        }

        Dispatcher BuildDispatcher()
        {
            var addressing = GetAddressing(settings, connectionString);
            var addressRetriever = GetAddressGenerator(settings);
            
            Func<UnicastTransportOperation, CancellationToken, Task<bool>> shouldSend;
            if (delayedDelivery != null)
            {
                shouldSend = delayedDelivery.ShouldDispatch;
            }
            else
            {
                shouldSend = (_, __) => Task.FromResult(true);
            }
            return new Dispatcher(addressRetriever, addressing, serializer, shouldSend);
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
            var maximumWaitTime = settings.Get<TimeSpan>(WellKnownConfigurationKeys.ReceiverMaximumWaitTimeWhenIdle);
            var peekInterval = settings.Get<TimeSpan>(WellKnownConfigurationKeys.ReceiverPeekInterval);

            string tableName;
            if (IsNativeDelayedDeliveryCongifured(out tableName))
            {
                nativeTimeoutsCancellationSource = new CancellationTokenSource();
                poller = new TimeoutsPoller(connectionString, BuildDispatcher(), tableName, new BackoffStrategy(maximumWaitTime, peekInterval));
                await poller.Start(settings, nativeTimeoutsCancellationSource.Token).ConfigureAwait(false);
                await delayedDelivery.Init().ConfigureAwait(false);
            }
        }

        public override Task Stop()
        {
            nativeTimeoutsCancellationSource?.Cancel();
            return poller != null ? poller.Stop() : TaskEx.CompletedTask;
        }

        bool IsNativeDelayedDeliveryCongifured(out string tableName)
        {
            if (string.IsNullOrEmpty(delayedDeliverySettings.Name))
            {
                tableName = null;
                return false;
            }

            tableName = delayedDeliverySettings.Name;
            return true;
        }

        readonly ReadOnlySettings settings;
        readonly string connectionString;
        readonly MessageWrapperSerializer serializer;
        readonly DelayedDeliverySettings delayedDeliverySettings;
        NativeDelayDelivery delayedDelivery;
        TimeoutsPoller poller;
        CancellationTokenSource nativeTimeoutsCancellationSource;
    }
}