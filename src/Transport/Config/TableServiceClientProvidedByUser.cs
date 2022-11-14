namespace NServiceBus.Transport.AzureStorageQueues
{
    using global::Azure.Data.Tables;

    class TableServiceClientProvidedByUser : ITableServiceClientProvider
    {
        public TableServiceClientProvidedByUser(TableServiceClient tableServiceClient) =>
            Client = tableServiceClient;

        public TableServiceClient Client { get; }
    }
}