﻿namespace NServiceBus.Transport.AzureStorageQueues
{
    static class WellKnownConfigurationKeys
    {
        //TODO: this seems to be used nowhere
        //public const string ReceiverConnectionString = "Transport.ConnectionString";
        public const string MessageWrapperSerializationDefinition = "Transport.AzureStorageQueue.MessageWrapperSerializationDefinition";
        public const string DegreeOfReceiveParallelism = "Transport.AzureStorageQueue.DegreeOfReceiveParallelism";

        public static class DelayedDelivery
        {
            public const string TableName = "Transport.AzureStorageQueue.TableName";
        }
    }
}