namespace NServiceBus.AzureStorageQueues
{
    using System;
    using Config;
    using Settings;

    static class DefaultConfigurationValues
    {
        public static void Apply(SettingsHolder settings)
        {
            settings.SetDefault(WellKnownConfigurationKeys.ReceiverConnectionString, DefaultConnectionString);
            settings.SetDefault(WellKnownConfigurationKeys.ReceiverMessageInvisibleTime, DefaultMessageInvisibleTime);
            settings.SetDefault(WellKnownConfigurationKeys.ReceiverPeekInterval, DefaultPeekInterval);
            settings.SetDefault(WellKnownConfigurationKeys.ReceiverMaximumWaitTimeWhenIdle, DefaultMaximumWaitTimeWhenIdle);
        }

        static TimeSpan DefaultMessageInvisibleTime = TimeSpan.FromSeconds(30);
        static TimeSpan DefaultPeekInterval = TimeSpan.FromMilliseconds(125);
        static TimeSpan DefaultMaximumWaitTimeWhenIdle = TimeSpan.FromSeconds(30);
        internal const int DefaultBatchSize = 32;
        const string DefaultConnectionString = "";
    }
}
