namespace NServiceBus.Transport.AzureStorageQueues
{
    static class WellKnownConfigurationKeys
    {
        //TODO: this seems to be used nowhere
        //public const string ReceiverConnectionString = "Transport.ConnectionString";

        public static class DelayedDelivery
        {
            public const string TableName = "Transport.AzureStorageQueue.TableName";
        }
    }
}