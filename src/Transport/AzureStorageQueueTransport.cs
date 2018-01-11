namespace NServiceBus
{
    using System;
    using System.Reflection;
    using AzureStorageQueues;
    using MessageInterfaces;
    using Routing;
    using Serialization;
    using Settings;
    using Transport;

    /// <summary>
    /// Transport definition for AzureStorageQueue
    /// </summary>
    public class AzureStorageQueueTransport : TransportDefinition, IMessageDrivenSubscriptionTransport
    {
        public override bool RequiresConnectionString { get; } = true;

        public override string ExampleConnectionStringForErrorMessage { get; } =
            "DefaultEndpointsProtocol=[http|https];AccountName=myAccountName;AccountKey=myAccountKey";

        public override TransportInfrastructure Initialize(SettingsHolder settings, string connectionString)
        {
            Guard.AgainstNull(nameof(settings), settings);
            Guard.AgainstNullAndEmpty(nameof(connectionString), connectionString);
            // configure JSON instead of XML as the default serializer:
            SetMainSerializer(settings, new JsonSerializer());

            DefaultConfigurationValues.Apply(settings);

            return new AzureStorageQueueInfrastructure(settings, connectionString);
        }

        static void SetMainSerializer(SettingsHolder settings, SerializationDefinition definition)
        {
            settings.SetDefault("MainSerializer", Tuple.Create(definition, new SettingsHolder()));
        }

        internal static IMessageSerializer GetMainSerializer(IMessageMapper mapper, ReadOnlySettings settings)
        {
            var definitionAndSettings = settings.Get<Tuple<SerializationDefinition, SettingsHolder>>("MainSerializer");
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