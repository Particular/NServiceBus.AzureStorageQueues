namespace NServiceBus
{
    using System;
    using System.Collections.Generic;
    using NServiceBus.Azure.Transports.WindowsAzureStorageQueues;
    using NServiceBus.AzureStorageQueues;

    public sealed class AzureStorageAddressingSettings 
    {
        private static readonly IEnumerable<string> headersToApplyNameMapping = new[]
        {
            Headers.ReplyToAddress
        };

        private readonly Dictionary<ConnectionString, string> _connectionString2name = new Dictionary<ConnectionString, string>();
        private readonly Dictionary<string, ConnectionString> _name2connectionString = new Dictionary<string, ConnectionString>();

        bool logicalQueueAddresses;

        public AzureStorageAddressingSettings AddStorageAccount(string name, string connectionString)
        {
            if (name == QueueAddress.DefaultStorageAccountName)
            {
                throw new ArgumentException("Don't use default empty name", nameof(name));
            }

            Add(name, connectionString);
            return this;
        }

        public AzureStorageAddressingSettings UseAccountNamesInsteadOfConnectionStrings()
        {
            logicalQueueAddresses = true;
            return this;
        }

        /// <summary>
        ///     Maps the account name to a connection string, throwing when no mapping found.
        /// </summary>
        internal ConnectionString Map(string name)
        {
            ConnectionString connectionString;
            if (_name2connectionString.TryGetValue(name, out connectionString) == false)
            {
                throw new KeyNotFoundException($"No account was mapped under following name '{name}'. Please map it using AddStorageAccount method.");
            }

            return connectionString;
        }

        /// <summary>
        ///     Transforms all the <see cref="QueueAddress" /> in <see cref="headersToApplyNameMapping" /> to use logical names.
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
                        if (_name2connectionString.ContainsKey(name) == false)
                        {
                            throw new KeyNotFoundException($"No account was mapped under following name '{name}'. Please map it using AddStorageAccount method.");
                        }
                    }
                }
            }
        }

        /// <summary>
        ///     Transforms all the <see cref="QueueAddress" /> in <see cref="headersToApplyNameMapping" /> to connection string
        ///     values to maintain backward compatibility.
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

        private bool TryMap(ConnectionString connectionString, out string name)
        {
            return _connectionString2name.TryGetValue(connectionString, out name);
        }

        internal void Add(string name, string connectionString, bool throwOnExisitingEntry = true)
        {
            var value = new ConnectionString(connectionString);
            if (throwOnExisitingEntry)
            {
                _name2connectionString.Add(name, value);
                _connectionString2name.Add(value, name);
            }
            else
            {
                _name2connectionString[name] = value;
                _connectionString2name[value] = name;
            }
        }
    }
}