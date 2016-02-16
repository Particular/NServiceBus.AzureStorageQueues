namespace NServiceBus
{
    using System.Collections.Generic;

    public class AzureStorageAddressingSettings : IAzureStorageNamespacePartitioningSettings
    {
        readonly Dictionary<string, string> _namespaces = new Dictionary<string, string>();

        IAzureStorageNamespacePartitioningSettings IAzureStorageNamespacePartitioningSettings.AddNamespace(string name, string connectionString)
        {
            _namespaces.Add(name, connectionString);
            return this;
        }

        /// <summary>
        ///     Tries to map the namespace to a connection string.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="connectionString"></param>
        /// <returns></returns>
        internal bool TryMapNamespace(string name, out string connectionString)
        {
            if (name == null)
            {
                connectionString = null;
                return false;
            }
            return _namespaces.TryGetValue(name, out connectionString);
        }
    }
}