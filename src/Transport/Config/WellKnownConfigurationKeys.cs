namespace NServiceBus.AzureStorageQueues.Config
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
        public const string DegreeOfReceiveParallelism = "Transport.AzureStorageQueue.DegreeOfReceiveParallelism";
        public const string UseAccountNamesInsteadOfConnectionStrings = "Transport.AzureStorageQueue.UseAccountAliasesInsteadOfConnectionStrings";
        
        public static class DelayedDelivery
        {
            public const string TableName = "Transport.AzureStorageQueue.TableName";
            public const string DisableTimeoutManager = "Transport.AzureStorageQueue.DisableTimeoutManager";
            public const string DisableDelayedDelivery = "Transport.AzureStorageQueue.DisableDelayedDelivery";
        }
    }
}