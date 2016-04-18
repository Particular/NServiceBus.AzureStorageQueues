namespace NServiceBus
{
    using System;
    using System.Collections.Generic;
    using Azure.Transports.WindowsAzureStorageQueues;
    using Azure.Transports.WindowsAzureStorageQueues.Config;

    public sealed class AzureStorageAddressingSettings : IAzureStoragePartitioningSettings
    {
        IAzureStoragePartitioningSettings IAzureStoragePartitioningSettings.AddStorageAccount(string name, string connectionString)
        {
            if (name == QueueAddress.DefaultStorageAccountName)
            {
                throw new ArgumentException("Don't use default empty name", nameof(name));
            }

            Add(name, connectionString);
            return this;
        }

        public IAzureStoragePartitioningSettings UseAccountNamesInsteadOfConnectionStrings()
        {
            logicalQueueAddresses = true;
            return this;
        }

        /// <summary>
        /// Maps the account name to a connection string, throwing when no mapping found.
        /// </summary>
        internal ConnectionString Map(string name)
        {
            ConnectionString connectionString;
            if (name2connectionString.TryGetValue(name, out connectionString) == false)
            {
                throw new KeyNotFoundException($"No account was mapped under following name '{name}'. Please map it using AddStorageAccount method.");
            }

            return connectionString;
        }

        /// <summary>
        /// Transforms all the <see cref="QueueAddress" /> in <see cref="headersToApplyNameMapping" /> to use logical names.
        /// </summary>
        internal void ApplyMappingToLogicalName(HeadersCollection headers)
        {
            foreach (var header in headersToApplyNameMapping)
            {
                string headerValue;
                if (headers.TryGetValue(header, out headerValue))
                {
                    var address = QueueAddress.Parse(headerValue);
                    string name;

                    // try map as connection string
                    if (TryMap(new ConnectionString(address.StorageAccount), out name))
                    {
                        headers[header] = new QueueAddress(address.QueueName, name).ToString();
                    }
                    else
                    {
                        // it must be a logical name, try to find it, otherwise throw
                        if (name2connectionString.ContainsKey(name) == false)
                        {
                            throw new KeyNotFoundException($"No account was mapped under following name '{name}'. Please map it using AddStorageAccount method.");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Transforms all the <see cref="QueueAddress" /> in <see cref="headersToApplyNameMapping" /> to connection string
        /// values to maintain backward compatibility.
        /// </summary>
        internal void ApplyMappingOnOutgoingHeaders(HeadersCollection headers)
        {
            if (logicalQueueAddresses)
            {
                return;
            }

            foreach (var header in headersToApplyNameMapping)
            {
                string headerValue;
                if (headers.TryGetValue(header, out headerValue))
                {
                    var address = QueueAddress.Parse(headerValue);
                    var connectionString = Map(address.StorageAccount);
                    headers[header] = new QueueAddress(address.QueueName, connectionString.Value).ToString();
                }
            }
        }

        bool TryMap(ConnectionString connectionString, out string name)
        {
            return connectionString2name.TryGetValue(connectionString, out name);
        }

        internal void Add(string name, string connectionString, bool throwOnExisitingEntry = true)
        {
            var value = new ConnectionString(connectionString);
            if (throwOnExisitingEntry)
            {
                name2connectionString.Add(name, value);
                connectionString2name.Add(value, name);
            }
            else
            {
                name2connectionString[name] = value;
                connectionString2name[value] = name;
            }
        }

        Dictionary<ConnectionString, string> connectionString2name = new Dictionary<ConnectionString, string>();
        Dictionary<string, ConnectionString> name2connectionString = new Dictionary<string, ConnectionString>();

        bool logicalQueueAddresses;

        static string[] headersToApplyNameMapping =
        {
            Headers.ReplyToAddress
        };
    }
}