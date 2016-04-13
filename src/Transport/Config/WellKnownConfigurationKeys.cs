namespace NServiceBus.Azure.Transports.WindowsAzureStorageQueues.Config
{
    static class WellKnownConfigurationKeys
    {
        public const string ReceiverConnectionString = "Transport.ConnectionString";
        public const string ReceiverMaximumWaitTimeWhenIdle = "Transport.AzureStorageQueue.ReceiverMaximumWaitTimeWhenIdle";
        public const string ReceiverPeekInterval = "Transport.AzureStorageQueue.ReceiverPeekInterval";
        public const string ReceiverMessageInvisibleTime = "Transport.AzureStorageQueue.Settings.ReceiverMessageInvisibleTime";
        public const string ReceiverBatchSize = "Transport.AzureStorageQueue.ReceiverBatchSize";
        public const string MessageWrapperSerializationDefinition = "Transport.AzureStorageQueue.MessageWrapperSerializationDefinition";
        public const string Sha1Shortener = "Transport.AzureStorageQueue.Sha1Shortener";
        public const string PurgeOnStartup = "Transport.AzureStorageQueue.PurgeOnStartup";
        public const string DefaultQueuePerInstance = "Transport.AzureStorageQueue.DefaultQueuePerInstance";
    }
}