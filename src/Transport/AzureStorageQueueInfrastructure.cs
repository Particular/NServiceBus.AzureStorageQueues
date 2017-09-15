namespace NServiceBus.AzureStorageQueues
{
    using System;
    using System.Collections.Generic;
    using System.Security.Cryptography;
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

            var timeoutManagerFeatureDisabled = settings.GetOrDefault<FeatureState>(typeof(TimeoutManager).FullName) == FeatureState.Disabled;
            var sendOnlyEndpoint = settings.GetOrDefault<bool>("Endpoint.SendOnly");

            if (timeoutManagerFeatureDisabled || sendOnlyEndpoint)
            {
                // TimeoutManager is already not used. Indicate to Native Delayed Delivery that we're not in the hybrid mode.
                settings.Set(WellKnownConfigurationKeys.DelayedDelivery.DisableTimeoutManager, true);
            }

            delayedDelivery = new NativeDelayDelivery(connectionString, GetDelayedDeliveryTableName(settings));
            addressGenerator = new QueueAddressGenerator(settings.GetOrDefault<Func<string, string>>(WellKnownConfigurationKeys.QueueSanitizer));
        }

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

        public override IEnumerable<Type> DeliveryConstraints
        {
            get
            {
                yield return typeof(DiscardIfNotReceivedBefore);
                yield return typeof(NonDurableDelivery);

                if (DelayedDeliveryCanBeUsed())
                {
                    yield return typeof(DoNotDeliverBefore);
                    yield return typeof(DelayDeliveryWith);
                }
            }
        }

        bool DelayedDeliveryCanBeUsed() => 
            settings.GetOrDefault<bool>(WellKnownConfigurationKeys.DelayedDelivery.DisableTimeoutManager)
            && settings.GetOrDefault<bool>(WellKnownConfigurationKeys.DelayedDelivery.DisableDelayedDelivery) == false;

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

                    var maximumWaitTime = settings.Get<TimeSpan>(WellKnownConfigurationKeys.ReceiverMaximumWaitTimeWhenIdle);
                    var peekInterval = settings.Get<TimeSpan>(WellKnownConfigurationKeys.ReceiverPeekInterval);

                    var receiver = new AzureMessageQueueReceiver(unwrapper, client, addressGenerator)
                    {
                        MessageInvisibleTime = settings.Get<TimeSpan>(WellKnownConfigurationKeys.ReceiverMessageInvisibleTime),

                        BatchSize = settings.Get<int>(WellKnownConfigurationKeys.ReceiverBatchSize)
                    };

                    int? degreeOfReceiveParallelism = null;
                    if (settings.TryGet<int>(WellKnownConfigurationKeys.DegreeOfReceiveParallelism, out var parallelism))
                    {
                        degreeOfReceiveParallelism = parallelism;
                    }

                    return new MessagePump(receiver, addressing, degreeOfReceiveParallelism, maximumWaitTime, peekInterval);
                },
                () => new AzureMessageQueueCreator(client, addressGenerator),
                () => Task.FromResult(StartupCheckResult.Success)
                );
        }

        static AzureStorageAddressingSettings GetAddressing(ReadOnlySettings settings, string connectionString)
        {
            var addressing = settings.GetOrDefault<AzureStorageAddressingSettings>() ?? new AzureStorageAddressingSettings();

            if (settings.TryGet<AccountConfigurations>(out var accounts) == false)
            {
                accounts = new AccountConfigurations();
            }

            var shouldUseAccountNames = settings.TryGet(WellKnownConfigurationKeys.UseAccountNamesInsteadOfConnectionStrings, out object _);

            addressing.RegisterMapping(accounts.defaultAlias, accounts.mappings, shouldUseAccountNames);
            addressing.Add(QueueAddress.DefaultStorageAccountAlias, connectionString, false);

            return addressing;
        }

        static MessageWrapperSerializer BuildSerializer(ReadOnlySettings settings)
        {
            if (settings.TryGet<SerializationDefinition>(WellKnownConfigurationKeys.MessageWrapperSerializationDefinition, out var wrapperSerializer))
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
            return new Dispatcher(addressGenerator, addressing, serializer, delayedDelivery.ShouldDispatch);
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

            return addressGenerator.GetQueueName(queue.ToString());
        }

        public override Task Start()
        {
            var maximumWaitTime = settings.Get<TimeSpan>(WellKnownConfigurationKeys.ReceiverMaximumWaitTimeWhenIdle);
            var peekInterval = settings.Get<TimeSpan>(WellKnownConfigurationKeys.ReceiverPeekInterval);
            poller = new DelayedMessagesPoller(delayedDelivery.Table, connectionString, BuildDispatcher(), new BackoffStrategy(maximumWaitTime, peekInterval));
            nativeDelayedMessagesCancellationSource = new CancellationTokenSource();
            poller.Start(settings, nativeDelayedMessagesCancellationSource.Token);
            return TaskEx.CompletedTask;
        }

        public override Task Stop()
        {
            nativeDelayedMessagesCancellationSource?.Cancel();
            return poller != null ? poller.Stop() : TaskEx.CompletedTask;
        }

        readonly ReadOnlySettings settings;
        readonly string connectionString;
        readonly MessageWrapperSerializer serializer;
        NativeDelayDelivery delayedDelivery;
        DelayedMessagesPoller poller;
        CancellationTokenSource nativeDelayedMessagesCancellationSource;
        QueueAddressGenerator addressGenerator;
    }
}