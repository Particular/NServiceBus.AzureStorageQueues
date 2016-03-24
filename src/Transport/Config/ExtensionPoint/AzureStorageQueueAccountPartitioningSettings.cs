namespace NServiceBus
{
    using NServiceBus.Azure.Transports.WindowsAzureStorageQueues.Config;
    using NServiceBus.AzureStorageQueue.Addressing;
    using NServiceBus.Configuration.AdvanceExtensibility;
    using NServiceBus.Settings;

    public class AzureStorageQueueAccountPartitioningSettings : ExposeSettings
    {
        private readonly SettingsHolder _settings;

        public AzureStorageQueueAccountPartitioningSettings(SettingsHolder settings) 
            : base(settings)
        {
            _settings = settings;
        }

        public AzureStorageQueueAccountPartitioningSettings UseStrategy<T>()
            where T : IAccountPartitioningStrategy
        {
            _settings.Set(WellKnownConfigurationKeys.Addressing.Partitioning.Strategy, typeof(T));

            return this;
        }
    }
}