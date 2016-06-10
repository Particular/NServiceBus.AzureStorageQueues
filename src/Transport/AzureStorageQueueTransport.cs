namespace NServiceBus
{
    using Azure.Transports.WindowsAzureStorageQueues;
    using AzureStorageQueues;
    using Serialization;
    using Settings;
    using Transport;

    /// <summary>
    /// Transport definition for AzureStorageQueue
    /// </summary>
    public class AzureStorageQueueTransport : TransportDefinition
    {
        public override bool RequiresConnectionString { get; } = true;

        public override string ExampleConnectionStringForErrorMessage { get; } =
            "DefaultEndpointsProtocol=[http|https];AccountName=myAccountName;AccountKey=myAccountKey";

        public override TransportInfrastructure Initialize(SettingsHolder settings, string connectionString)
        {
            // configure JSON instead of XML as the default serializer:
            settings.SetDefault<SerializationDefinition>(new JsonSerializer());

            // register the MessageWrapper as a system message to have it registered in mappings and serializers
            settings.GetOrCreate<Conventions>().AddSystemMessagesConventions(t => t == typeof(MessageWrapper));

            new DefaultConfigurationValues().Apply(settings);

            return new AzureStorageQueueInfrastructure(settings, connectionString);
        }
    }
}