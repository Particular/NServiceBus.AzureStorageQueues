﻿namespace NServiceBus.Transport.AzureStorageQueues
{
    using System;
    using Settings;

    static class DefaultConfigurationValues
    {
        public static void Apply(SettingsHolder settings)
        {
            //TODO: this seems to be used nowhere
            //settings.SetDefault(WellKnownConfigurationKeys.ReceiverConnectionString, DefaultConnectionString);
            settings.SetDefault(WellKnownConfigurationKeys.ReceiverMaximumWaitTimeWhenIdle, DefaultMaximumWaitTimeWhenIdle);
        }

        internal static readonly Func<string, string> DefaultQueueNameSanitizer = entityName => entityName;
        internal static readonly TimeSpan DefaultMessageInvisibleTime = TimeSpan.FromSeconds(30);
        static TimeSpan DefaultPeekInterval = TimeSpan.FromMilliseconds(125);
        static TimeSpan DefaultMaximumWaitTimeWhenIdle = TimeSpan.FromSeconds(30);
        internal const int DefaultBatchSize = 32;
        const string DefaultConnectionString = "";
    }
}
