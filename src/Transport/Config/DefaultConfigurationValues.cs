namespace NServiceBus.AzureStorageQueues
{
    using System;
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

        TimeSpan DefaultMessageInvisibleTime = TimeSpan.FromSeconds(30);
        TimeSpan DefaultPeekInterval = TimeSpan.FromMilliseconds(125);
        TimeSpan DefaultMaximumWaitTimeWhenIdle = TimeSpan.FromSeconds(1);
        const int DefaultBatchSize = 32;
        const string DefaultConnectionString = "";
    }
}
