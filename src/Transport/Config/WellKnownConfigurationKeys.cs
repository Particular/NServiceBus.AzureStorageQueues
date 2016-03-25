namespace NServiceBus.AzureStorageQueues
{
    public static class WellKnownConfigurationKeys
    {
        public const string ReceiverConnectionString = "Transport.ConnectionString";
        public const string ReceiverMaximumWaitTimeWhenIdle = "Transport.AzureStorageQueue.ReceiverMaximumWaitTimeWhenIdle";
        public const string ReceiverPeekInterval = "Transport.AzureStorageQueue.ReceiverPeekInterval";
        public const string ReceiverMessageInvisibleTime = "Transport.AzureStorageQueue.Settings.ReceiverMessageInvisibleTime";
        public const string ReceiverBatchSize = "Transport.AzureStorageQueue.ReceiverBatchSize";
        public const string MessageWrapperSerializer = "Transport.AzureStorageQueue.MessageWrapperSerializer";
        public const string MessageWrapperSerializerFactory = "Transport.AzureStorageQueue.MessageWrapperSerializerFactory";
        public const string Sha1Shortener = "Transport.AzureStorageQueue.Sha1Shortener";
        public const string PurgeOnStartup = "Transport.AzureStorageQueue.PurgeOnStartup";
        public const string DefaultQueuePerInstance = "Transport.AzureStorageQueue.DefaultQueuePerInstance";

        public static class Addressing
        {
            public static class Partitioning
            {
                public const string Accounts = "Transport.AzureStorageQueue.Addressing.Partitioning.Accounts";
                public const string Strategy = "Transport.AzureStorageQueue.Addressing.Partitioning.Strategy";
            }
        }
    }
}
