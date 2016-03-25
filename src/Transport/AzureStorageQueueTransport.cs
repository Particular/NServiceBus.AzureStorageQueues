namespace NServiceBus
{
    using NServiceBus.Azure.Transports.WindowsAzureStorageQueues;
    using NServiceBus.AzureStorageQueues;
    using NServiceBus.Settings;
    using NServiceBus.Transports;

    /// <summary>
    ///     Transport definition for AzureStorageQueue
    /// </summary>
    public class AzureStorageQueueTransport : TransportDefinition
    {
        protected override TransportInfrastructure Initialize(SettingsHolder settings, string connectionString)
        {
            settings.SetDefault("Transactions.DoNotWrapHandlersExecutionInATransactionScope", true);
            settings.SetDefault("Transactions.SuppressDistributedTransactions", true);
            new DefaultConfigurationValues().Apply(settings);

            RegisterConnectionStringAsStorageAccount(settings, connectionString);

            return new AzureStorageQueueInfrastructure(settings, connectionString);
        }

        public override bool RequiresConnectionString { get; } = true;

        public override string ExampleConnectionStringForErrorMessage { get; } =
            "DefaultEndpointsProtocol=[http|https];AccountName=myAccountName;AccountKey=myAccountKey";

        void RegisterConnectionStringAsStorageAccount(SettingsHolder settings, string connectionString)
        {
            var extensions = new AzureStorageQueueAccountPartitioningSettings(settings);
            extensions.AddStorageAccount("default", connectionString);
        }
    }
}