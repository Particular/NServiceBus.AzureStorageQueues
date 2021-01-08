namespace NServiceBus.Transport.AzureStorageQueues
{
    using System;
    using Settings;

    static class DefaultConfigurationValues
    {
        public static void Apply(SettingsHolder settings)
        {
            //TODO: this seems to be used nowhere
            //settings.SetDefault(WellKnownConfigurationKeys.ReceiverConnectionString, DefaultConnectionString);
            settings.SetDefault(WellKnownConfigurationKeys.ReceiverPeekInterval, DefaultPeekInterval);
            settings.SetDefault(WellKnownConfigurationKeys.ReceiverMaximumWaitTimeWhenIdle, DefaultMaximumWaitTimeWhenIdle);
            settings.SetDefault(WellKnownConfigurationKeys.QueueSanitizer, (Func<string, string>)(queueName => queueName));
        }

        internal static TimeSpan DefaultMessageInvisibleTime = TimeSpan.FromSeconds(30);
        static TimeSpan DefaultPeekInterval = TimeSpan.FromMilliseconds(125);
        static TimeSpan DefaultMaximumWaitTimeWhenIdle = TimeSpan.FromSeconds(30);
        internal const int DefaultBatchSize = 32;
        const string DefaultConnectionString = "";
    }
}
