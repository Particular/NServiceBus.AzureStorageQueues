namespace NServiceBus
{
    using global::Azure.Storage.Queues;
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using MessageInterfaces;
    using Serialization;
    using Settings;
    using Transport;
    using Transport.AzureStorageQueues;

    /// <summary>
    /// Transport definition for AzureStorageQueue
    /// </summary>
    public class AzureStorageQueueTransport : TransportDefinition
    {
        internal const string SerializerSettingsKey = "MainSerializer";

        internal static IMessageSerializer GetMainSerializer(IMessageMapper mapper, ReadOnlySettings settings)
        {
            var definitionAndSettings = settings.Get<Tuple<SerializationDefinition, SettingsHolder>>(SerializerSettingsKey);
            var definition = definitionAndSettings.Item1;
            var serializerSettings = definitionAndSettings.Item2;

            // serializerSettings.Merge(settings);
            var merge = typeof(SettingsHolder).GetMethod("Merge", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            merge.Invoke(serializerSettings, new object[]
            {
                settings
            });

            var serializerFactory = definition.Configure(serializerSettings);
            var serializer = serializerFactory(mapper);
            return serializer;
        }

        /// <summary>
        /// Initialize a new transport definition for AzureStorageQueue
        /// </summary>
        public AzureStorageQueueTransport(string connectionString) : base(TransportTransactionMode.ReceiveOnly)
        {
            //TODO: store the connection string
        }

        /// <summary>
        /// Initialize a new transport definition for AzureStorageQueue
        /// </summary>
        public AzureStorageQueueTransport(QueueClient queueClient) : base(TransportTransactionMode.ReceiveOnly)
        {
            //TODO: store the queue client
        }

        public override Task<TransportInfrastructure> Initialize(HostSettings hostSettings, ReceiveSettings[] receivers, string[] sendingAddresses,
            CancellationToken cancellationToken = new CancellationToken())
        {
            Guard.AgainstNull(nameof(hostSettings), hostSettings);
            Guard.AgainstNull(nameof(receivers), receivers);
            Guard.AgainstNull(nameof(sendingAddresses), sendingAddresses);

            //TODO: understand how to retrieve the endpoint serializer, investigate if it's needed at this stage
            Guard.AgainstUnsetSerializerSetting(settings);

            //TODO: move these to (public?) properties
            DefaultConfigurationValues.Apply(settings);

            return new AzureStorageQueueInfrastructure(settings, connectionString);
        }

        public override string ToTransportAddress(Transport.QueueAddress address)
        {
            //TODO: move here the QueueAddressGenerator and code from AzureStorageQueueInfrastructure.ToTransportAddress
            throw new NotImplementedException();
        }

        /// <inheritdoc cref="GetSupportedTransactionModes"/>
        public override IReadOnlyCollection<TransportTransactionMode> GetSupportedTransactionModes()
        {
            return supportedTransactionModes;
        }

        /// <inheritdoc cref="SupportsDelayedDelivery"/>
        public override bool SupportsDelayedDelivery { get; } = true;

        /// <inheritdoc cref="SupportsPublishSubscribe"/>
        public override bool SupportsPublishSubscribe { get; } = false;

        /// <inheritdoc cref="SupportsTTBR"/>
        public override bool SupportsTTBR { get; } = false;

        private readonly TransportTransactionMode[] supportedTransactionModes = new[] {TransportTransactionMode.None, TransportTransactionMode.ReceiveOnly};
    }
}