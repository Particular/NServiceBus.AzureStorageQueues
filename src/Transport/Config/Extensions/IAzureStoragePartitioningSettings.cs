namespace NServiceBus
{
    public interface IAzureStoragePartitioningSettings
    {
        /// <summary>
        ///     Adds a mapping between a logical name and a connection string.
        /// </summary>
        IAzureStoragePartitioningSettings AddStorageAccount(string name, string connectionString);
    }
}