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
        /// Maps the account name to a connection string, throwing when no mapping found.
        /// </summary>
        internal string MapAccountToConnectionString(string name)
        {
            string connectionString;
            if (_accounts.TryGetValue(name, out connectionString) == false)
            {
                throw new KeyNotFoundException($"No account was mapped under following name '{name}'. Please map it using AddStorageAccount method.");
            }

            return connectionString;
        }

        internal void SetDefaultAccountConnectionString(string connectionString)
        {
            _accounts[QueueAtAccount.DefaultStorageAccountName] = connectionString;
        }
    }
}