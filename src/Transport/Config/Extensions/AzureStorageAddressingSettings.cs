namespace NServiceBus
{
    using System;
    using System.Collections.Generic;
    using NServiceBus.Azure.Transports.WindowsAzureStorageQueues.Config;

    public sealed class AzureStorageAddressingSettings : IAzureStoragePartitioningSettings
    {
        readonly Dictionary<string, string> _accounts = new Dictionary<string, string>();

        IAzureStoragePartitioningSettings IAzureStoragePartitioningSettings.AddStorageAccount(string name, string connectionString)
        {
            if (name == QueueAtAccount.DefaultStorageAccountName)
            {
                throw new ArgumentException("Don't use default empty name", nameof(name));
            }

            _accounts.Add(name, connectionString);
            return this;
        }

        /// <summary>
        ///     Tries to map the namespace to a connection string.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="connectionString"></param>
        /// <returns></returns>
        internal bool TryMapAccount(string name, out string connectionString)
        {
            if (name == null)
            {
                connectionString = null;
                return false;
            }
            return _accounts.TryGetValue(name, out connectionString);
        }

        internal void SetDefaultAccountConnectionString(string connectionString)
        {
            _accounts[QueueAtAccount.DefaultStorageAccountName] = connectionString;
        }
    }
}