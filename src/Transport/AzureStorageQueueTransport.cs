namespace NServiceBus
{
    using System;
    using System.Reflection;
    using Azure.Transports.WindowsAzureStorageQueues;
    using AzureStorageQueues;
    using MessageInterfaces;
    using Routing;
    using Serialization;
    using Settings;
    using Transport;
    using Unicast.Messages;

    /// <summary>
    /// Transport definition for AzureStorageQueue
    /// </summary>
    public class AzureStorageQueueTransport : TransportDefinition, IMessageDrivenSubscriptionTransport
    {
        internal const string SerializerSettingsKey = "MainSerializer";

        /// <inheritdoc cref="RequiresConnectionString"/>
        public override bool RequiresConnectionString { get; } = true;

        /// <inheritdoc cref="ExampleConnectionStringForErrorMessage"/>
        public override string ExampleConnectionStringForErrorMessage { get; } =
            "DefaultEndpointsProtocol=[http|https];AccountName=myAccountName;AccountKey=myAccountKey";

        /// <inheritdoc cref="Initialize"/>
        public override TransportInfrastructure Initialize(SettingsHolder settings, string connectionString)
        {
            Guard.AgainstNull(nameof(settings), settings);
            Guard.AgainstNullAndEmpty(nameof(connectionString), connectionString);

            Guard.AgainstUnsetSerializerSetting(settings);

            // register the MessageWrapper as a system message to have it registered in mappings and serializers
            settings.GetOrCreate<Conventions>().AddSystemMessagesConventions(t => t == typeof(MessageWrapper));

            // register metadata of the wrapper for the sake of XML serializer
            settings.Get<MessageMetadataRegistry>().GetMessageMetadata(typeof(MessageWrapper));

            DefaultConfigurationValues.Apply(settings);

            return new AzureStorageQueueInfrastructure(settings, connectionString);
        }

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
    }
}