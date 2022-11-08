namespace NServiceBus.Transport.AzureStorageQueues
{
    using global::Azure.Data.Tables;

    class TableServiceClientByConnectionString : ITableServiceClientProvider
    {
        public TableServiceClientByConnectionString(string connectionString)
        {
            ConnectionStringValidator.ThrowIfPremiumEndpointConnectionString(connectionString);

            Client = new TableServiceClient(connectionString);
        }

        public TableServiceClient Client { get; }
    }
}