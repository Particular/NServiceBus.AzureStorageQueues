namespace NServiceBus
{
    public interface IAzureStorageNamespacePartitioningSettings
    {
        /// <summary>
        ///     Adds a mapping between logical namespace name and a connection string.
        /// </summary>
        IAzureStorageNamespacePartitioningSettings AddNamespace(string name, string connectionString);
    }
}