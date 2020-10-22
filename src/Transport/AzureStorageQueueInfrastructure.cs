﻿using Azure.Storage.Blobs;

namespace NServiceBus.Transport.AzureStorageQueues
{
    using System;
    using System.Collections.Generic;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using DelayedDelivery;
    using Features;
    using Logging;
    using Microsoft.Azure.Cosmos.Table;
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

            if (IsPremiumEndpoint(connectionString))
            {
                throw new Exception($"When configuring {nameof(AzureStorageQueueTransport)} with a single connection string, only Azure Storage connection can be used. See documentation for alternative options to configure the transport.");
            }

            serializer = BuildSerializer(settings);

            settings.SetDefault(WellKnownConfigurationKeys.DelayedDelivery.EnableTimeoutManager, true);

            if (!settings.IsFeatureEnabled(typeof(TimeoutManager)) || settings.GetOrDefault<bool>("Endpoint.SendOnly"))
            {
                // TimeoutManager is already not used. Indicate to Native Delayed Delivery that we're not in the hybrid mode.
                settings.Set(WellKnownConfigurationKeys.DelayedDelivery.EnableTimeoutManager, false);
            }

            if (!settings.TryGet<CloudTableClient>(WellKnownConfigurationKeys.CloudTableClient, out var cloudTableClient))
            {
                var cloudStorageAccount = CloudStorageAccount.Parse(connectionString);
                cloudTableClient = cloudStorageAccount.CreateCloudTableClient();
            }

            if (!settings.TryGet<BlobServiceClient>(WellKnownConfigurationKeys.BlobServiceClient, out var blobServiceClient))
            {
                blobServiceClient = new BlobServiceClient(connectionString);
            }

            nativeDelayedDelivery = new NativeDelayDelivery(
                cloudTableClient,
                blobServiceClient,
                GetDelayedDeliveryTableName(settings),
                NativeDelayedDeliveryIsEnabled(),
                settings.ErrorQueueAddress(),
                GetRequiredTransactionMode(),
                settings.Get<TimeSpan>(WellKnownConfigurationKeys.ReceiverMaximumWaitTimeWhenIdle),
                settings.Get<TimeSpan>(WellKnownConfigurationKeys.ReceiverPeekInterval),
                BuildDispatcher);

            addressGenerator = new QueueAddressGenerator(settings.GetOrDefault<Func<string, string>>(WellKnownConfigurationKeys.QueueSanitizer));
        }

        // the SDK uses similar method of changing the underlying executor
        static bool IsPremiumEndpoint(string connectionString)
        {
            var lowerInvariant = connectionString.ToLowerInvariant();
            return lowerInvariant.Contains("https://localhost") || lowerInvariant.Contains(".table.cosmosdb.") || lowerInvariant.Contains(".table.cosmos.");
        }

        public override IEnumerable<Type> DeliveryConstraints => new List<Type> {typeof(DiscardIfNotReceivedBefore), typeof(NonDurableDelivery), typeof(DoNotDeliverBefore), typeof(DelayDeliveryWith)};

        public override TransportTransactionMode TransactionMode { get; } = TransportTransactionMode.ReceiveOnly;
        public override OutboundRoutingPolicy OutboundRoutingPolicy { get; } = new OutboundRoutingPolicy(OutboundRoutingType.Unicast, OutboundRoutingType.Unicast, OutboundRoutingType.Unicast);

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

        bool NativeDelayedDeliveryIsEnabled() => settings.GetOrDefault<bool>(WellKnownConfigurationKeys.DelayedDelivery.DisableDelayedDelivery) == false;

        public override TransportReceiveInfrastructure ConfigureReceiveInfrastructure()
        {
            Logger.Debug("Configuring receive infrastructure");
            var connectionObject = new ConnectionString(connectionString);
            var client = CreateQueueClients.CreateReceiver(connectionObject);

            return new TransportReceiveInfrastructure(
                () =>
                {
                    var addressing = GetAddressing(settings, connectionString);

                    var unwrapper = settings.HasSetting<IMessageEnvelopeUnwrapper>() ? settings.GetOrDefault<IMessageEnvelopeUnwrapper>() : new DefaultMessageEnvelopeUnwrapper(serializer);

                    var maximumWaitTime = settings.Get<TimeSpan>(WellKnownConfigurationKeys.ReceiverMaximumWaitTimeWhenIdle);
                    var peekInterval = settings.Get<TimeSpan>(WellKnownConfigurationKeys.ReceiverPeekInterval);

                    var receiver = new AzureMessageQueueReceiver(unwrapper, client, addressGenerator)
                    {
                        MessageInvisibleTime = settings.Get<TimeSpan>(WellKnownConfigurationKeys.ReceiverMessageInvisibleTime),
                    };

                    int? degreeOfReceiveParallelism = null;
                    if (settings.TryGet<int>(WellKnownConfigurationKeys.DegreeOfReceiveParallelism, out var parallelism))
                    {
                        degreeOfReceiveParallelism = parallelism;
                    }

                    int? batchSize = null;
                    if (settings.TryGet<int>(WellKnownConfigurationKeys.ReceiverBatchSize, out var size))
                    {
                        batchSize = size;
                    }

                    return new MessagePump(receiver, addressing, degreeOfReceiveParallelism, batchSize, maximumWaitTime, peekInterval);
                },
                () => new AzureMessageQueueCreator(client, addressGenerator),
                () => Task.FromResult(StartupCheckResult.Success)
            );
        }

        AzureStorageAddressingSettings GetAddressing(ReadOnlySettings settings, string connectionString)
        {
            var addressing = settings.GetOrDefault<AzureStorageAddressingSettings>() ?? new AzureStorageAddressingSettings();

            if (settings.TryGet<AccountConfigurations>(out var accounts) == false)
            {
                accounts = new AccountConfigurations();
            }

            var shouldUseAccountNames = settings.TryGet(WellKnownConfigurationKeys.UseAccountNamesInsteadOfConnectionStrings, out object _);

            addressing.SetAddressGenerator(addressGenerator);
            addressing.RegisterMapping(accounts.defaultAlias, accounts.mappings, shouldUseAccountNames);
            addressing.Add(new AccountInfo(QueueAddress.DefaultStorageAccountAlias, connectionString), false);

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
            return new Dispatcher(addressGenerator, addressing, serializer, nativeDelayedDelivery.ShouldDispatch);
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
                queue.Append("-" + logicalAddress.Qualifier);
            }

            return addressGenerator.GetQueueName(queue.ToString());
        }

        public override Task Start()
        {
            return nativeDelayedDelivery.Start();
        }

        public override Task Stop()
        {
            return nativeDelayedDelivery.Stop();
        }

        TransportTransactionMode GetRequiredTransactionMode()
        {
            var transportTransactionSupport = TransactionMode;

            //if user haven't asked for a explicit level use what the transport supports
            if (!settings.TryGet(out TransportTransactionMode requestedTransportTransactionMode))
            {
                return transportTransactionSupport;
            }

            if (requestedTransportTransactionMode > transportTransactionSupport)
            {
                throw new Exception($"Requested transaction mode `{requestedTransportTransactionMode}` can't be satisfied since the transport only supports `{transportTransactionSupport}`");
            }

            return requestedTransportTransactionMode;
        }

        readonly ReadOnlySettings settings;
        readonly string connectionString;
        readonly MessageWrapperSerializer serializer;
        NativeDelayDelivery nativeDelayedDelivery;
        QueueAddressGenerator addressGenerator;

        static readonly ILog Logger = LogManager.GetLogger<AzureStorageQueueInfrastructure>();
    }
}