namespace NServiceBus.AzureStorageQueues
{
    using NServiceBus.Azure.Transports.WindowsAzureStorageQueues.Config;
    using NServiceBus.Settings;

    class DefaultConfigurationValues
    {
        const int DefaultMessageInvisibleTime = 30000;
        const int DefaultPeekInterval = 50;
        const int DefaultMaximumWaitTimeWhenIdle = 1000;
        const int DefaultBatchSize = 10;
        const bool DefaultPurgeOnStartup = false;
        const string DefaultConnectionString = "";
        const bool DefaultQueuePerInstance = false;

        public void Apply(SettingsHolder settings)
        {
            ApplyDefaults(settings);
        }

        void ApplyDefaults(SettingsHolder settings)
        {
            settings.SetDefault(WellKnownConfigurationKeys.ReceiverConnectionString, DefaultConnectionString);
            settings.SetDefault(WellKnownConfigurationKeys.ReceiverMessageInvisibleTime, DefaultMessageInvisibleTime);
            settings.SetDefault(WellKnownConfigurationKeys.ReceiverPeekInterval, DefaultPeekInterval);
            settings.SetDefault(WellKnownConfigurationKeys.ReceiverMaximumWaitTimeWhenIdle, DefaultMaximumWaitTimeWhenIdle);
            settings.SetDefault(WellKnownConfigurationKeys.ReceiverBatchSize, DefaultBatchSize);
            settings.SetDefault(WellKnownConfigurationKeys.PurgeOnStartup, DefaultPurgeOnStartup);
            settings.SetDefault(WellKnownConfigurationKeys.DefaultQueuePerInstance, DefaultQueuePerInstance);
        }
    }
}
