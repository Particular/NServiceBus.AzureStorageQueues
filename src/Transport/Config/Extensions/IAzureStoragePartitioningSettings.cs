namespace NServiceBus
{
    public interface IAzureStoragePartitioningSettings
    {
        /// <summary>
        ///     Adds a mapping between a logical name and a connection string.
        /// </summary>
        IAzureStoragePartitioningSettings AddStorageAccount(string name, string connectionString);

        /// <summary>
        /// Configures this endpoint as one, using logical names of the accounts instead of connection strings when sending.
        /// </summary>
        IAzureStoragePartitioningSettings UseLogicalQueueAddresses();
    }
}