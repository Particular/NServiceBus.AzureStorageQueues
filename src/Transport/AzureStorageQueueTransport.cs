namespace NServiceBus
{
    using AzureStorageQueues;
    using Serialization;
    using Settings;
    using Transports;

    /// <summary>
    /// Transport definition for AzureStorageQueue
    /// </summary>
    public class AzureStorageQueueTransport : TransportDefinition
    {
        public override bool RequiresConnectionString { get; } = true;

        public override string ExampleConnectionStringForErrorMessage { get; } =
            "DefaultEndpointsProtocol=[http|https];AccountName=myAccountName;AccountKey=myAccountKey";

        protected override TransportInfrastructure Initialize(SettingsHolder settings, string connectionString)
        {
            settings.SetDefault("Transactions.DoNotWrapHandlersExecutionInATransactionScope", true);
            settings.SetDefault("Transactions.SuppressDistributedTransactions", true);
            
            // user JSON instead of XML as the default serializer:
            settings.SetDefault<SerializationDefinition>(new JsonSerializer());

            new DefaultConfigurationValues().Apply(settings);

            return new AzureStorageQueueInfrastructure(settings, connectionString);
        }
    }
}