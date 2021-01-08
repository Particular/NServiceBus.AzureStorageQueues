﻿namespace NServiceBus.Transport.AzureStorageQueues
{
    static class WellKnownConfigurationKeys
    {
        //TODO: this seems to be used nowhere
        //public const string ReceiverConnectionString = "Transport.ConnectionString";
        public const string ReceiverMaximumWaitTimeWhenIdle = "Transport.AzureStorageQueue.ReceiverMaximumWaitTimeWhenIdle";
        public const string ReceiverPeekInterval = "Transport.AzureStorageQueue.ReceiverPeekInterval";
        //TODO: moved to TransportDef.MessageInvisibleTime
        //public const string ReceiverMessageInvisibleTime = "Transport.AzureStorageQueue.Settings.ReceiverMessageInvisibleTime";
        public const string ReceiverBatchSize = "Transport.AzureStorageQueue.ReceiverBatchSize";
        public const string MessageWrapperSerializationDefinition = "Transport.AzureStorageQueue.MessageWrapperSerializationDefinition";
        public const string DegreeOfReceiveParallelism = "Transport.AzureStorageQueue.DegreeOfReceiveParallelism";
        public const string QueueSanitizer = "Transport.AzureStorageQueue.QueueSanitizer";

        public static class DelayedDelivery
        {
            public const string TableName = "Transport.AzureStorageQueue.TableName";
            public const string DisableDelayedDelivery = "Transport.AzureStorageQueue.DisableDelayedDelivery";
        }
    }
}