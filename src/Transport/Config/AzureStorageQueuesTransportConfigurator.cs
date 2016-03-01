namespace NServiceBus.AzureStorageQueues
{
    using NServiceBus.Azure.Transports.WindowsAzureStorageQueues.Config;
    using NServiceBus.Config;
    using NServiceBus.Features;
    using NServiceBus.Logging;
    using NServiceBus.Settings;

    public class AzureStorageQueuesTransportConfigurator : Feature
    {
        ILog logger = LogManager.GetLogger<AzureStorageQueuesTransportConfigurator>();

        public AzureStorageQueuesTransportConfigurator()
        {
            EnableByDefault();

            Defaults(settings =>
            {
                settings.SetDefault("Transactions.DoNotWrapHandlersExecutionInATransactionScope", true);
                settings.SetDefault("Transactions.SuppressDistributedTransactions", true);
                new DefaultConfigurationValues().Apply(settings);

            });

        }

        protected override void Setup(FeatureConfigurationContext context)
        {
        }
    }
}
