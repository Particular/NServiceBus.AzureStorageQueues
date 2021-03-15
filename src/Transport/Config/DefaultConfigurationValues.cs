namespace NServiceBus.Transport.AzureStorageQueues
{
    using System;
    using Settings;

    static class DefaultConfigurationValues
    {
        public static void Apply(SettingsHolder settings)
        {
            settings.SetDefault(WellKnownConfigurationKeys.ReceiverConnectionString, DefaultConnectionString);
            settings.SetDefault(WellKnownConfigurationKeys.ReceiverMessageInvisibleTime, DefaultMessageInvisibleTime);
            settings.SetDefault(WellKnownConfigurationKeys.ReceiverPeekInterval, DefaultPeekInterval);
            settings.SetDefault(WellKnownConfigurationKeys.ReceiverMaximumWaitTimeWhenIdle, DefaultMaximumWaitTimeWhenIdle);
            settings.SetDefault(WellKnownConfigurationKeys.QueueSanitizer, (Func<string, string>)(queueName => queueName));
            settings.SetDefault(WellKnownConfigurationKeys.PubSub.TableName, "subscriptions");
            settings.SetDefault(WellKnownConfigurationKeys.PubSub.DisableCaching, false);
            settings.SetDefault(WellKnownConfigurationKeys.PubSub.CacheInvalidationPeriod, cacheInvalidationPeriod);
        }

        static TimeSpan DefaultMessageInvisibleTime = TimeSpan.FromSeconds(30);
        static TimeSpan DefaultPeekInterval = TimeSpan.FromMilliseconds(125);
        static TimeSpan DefaultMaximumWaitTimeWhenIdle = TimeSpan.FromSeconds(30);
        internal const int DefaultBatchSize = 32;
        const string DefaultConnectionString = "";

        /// <summary>
        ///     Default to 5 seconds caching. If a system is under load that prevent doing an extra roundtrip for each Publish
        ///     operation. If
        ///     a system is not under load, doing an extra roundtrip every 5 seconds is not a problem and 5 seconds is small enough
        ///     value that
        ///     people accepts as we always say that subscription operation is not instantaneous.
        /// </summary>
        static TimeSpan cacheInvalidationPeriod = TimeSpan.FromSeconds(5);
    }
}
