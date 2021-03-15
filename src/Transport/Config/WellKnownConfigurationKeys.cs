namespace NServiceBus.Transport.AzureStorageQueues
{
    static class WellKnownConfigurationKeys
    {
        public const string ReceiverConnectionString = "Transport.ConnectionString";
        public const string ReceiverMaximumWaitTimeWhenIdle = "Transport.AzureStorageQueue.ReceiverMaximumWaitTimeWhenIdle";
        public const string ReceiverPeekInterval = "Transport.AzureStorageQueue.ReceiverPeekInterval";
        public const string ReceiverMessageInvisibleTime = "Transport.AzureStorageQueue.Settings.ReceiverMessageInvisibleTime";
        public const string ReceiverBatchSize = "Transport.AzureStorageQueue.ReceiverBatchSize";
        public const string MessageWrapperSerializationDefinition = "Transport.AzureStorageQueue.MessageWrapperSerializationDefinition";
        public const string DegreeOfReceiveParallelism = "Transport.AzureStorageQueue.DegreeOfReceiveParallelism";
        public const string QueueSanitizer = "Transport.AzureStorageQueue.QueueSanitizer";

        public static class DelayedDelivery
        {
            public const string TableName = "Transport.AzureStorageQueue.DelayedDeliveryTableName";
            public const string DisableDelayedDelivery = "Transport.AzureStorageQueue.DisableDelayedDelivery";
            public const string EnableTimeoutManager = "NServiceBus.TimeoutManager.EnableMigrationMode";
        }

        public static class PubSub
        {
            public const string DisablePublishSubscribe = "Transport.AzureStorageQueue.DisablePublishSubscribe";
            public const string DisableCaching = "Transport.AzureStorageQueue.DisableCaching";
            public const string TableName = "Transport.AzureStorageQueue.SubscriptionTableName";
            public const string CacheInvalidationPeriod = "Transport.AzureStorageQueue.CacheInvalidationPeriod";
        }
    }
}