namespace NServiceBus.Transport.AzureStorageQueues
{
    struct ReceiverConfiguration
    {
        public ReceiverConfiguration(int batchSize)
        {
            BatchSize = batchSize;
        }

        public int BatchSize { get; }
    }
}