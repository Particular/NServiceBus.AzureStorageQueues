namespace NServiceBus.AzureStorageQueues
{
    using Config;
    using Settings;

    class DefaultConfigurationValues
    {
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
        }

        const int DefaultMessageInvisibleTime = 30000;
        const int DefaultPeekInterval = 50;
        const int DefaultMaximumWaitTimeWhenIdle = 1000;
        const int DefaultBatchSize = 32;
        const string DefaultConnectionString = "";
    }
}