namespace NServiceBus.Transport.AzureStorageQueues
{
    using System;

    static class DefaultConfigurationValues
    {
        internal static readonly Func<string, string> DefaultQueueNameSanitizer = entityName => entityName;
        internal static readonly TimeSpan DefaultMessageInvisibleTime = TimeSpan.FromSeconds(30);
        internal static readonly TimeSpan DefaultPeekInterval = TimeSpan.FromMilliseconds(125);
        internal static readonly TimeSpan DefaultMaximumWaitTimeWhenIdle = TimeSpan.FromSeconds(30);
        internal const int DefaultBatchSize = 32;
    }
}
