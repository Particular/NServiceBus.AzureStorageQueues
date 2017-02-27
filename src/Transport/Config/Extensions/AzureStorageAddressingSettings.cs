namespace NServiceBus
{
    using System;
    using System.Collections.Generic;
    using AzureStorageQueues;
    using AzureStorageQueues.Config;

    sealed class AzureStorageAddressingSettings
    {
        internal void RegisterMapping(string defaultConnectionStringAlias, Dictionary<string, string> aliasToConnectionStringMap, bool shouldUseAccountAliases)
        {
            var hasAnyMapping = aliasToConnectionStringMap != null && aliasToConnectionStringMap.Count > 0;
            if (hasAnyMapping == false)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(defaultConnectionStringAlias))
            {
                throw new Exception("The mapping of account names instead of connection strings is enabled, but the default connection string name isn\'t provided. Provide the default connection string name when adding more accounts");
            }

            this.defaultConnectionStringAlias = defaultConnectionStringAlias;
            useLogicalQueueAddresses = shouldUseAccountAliases;

            foreach (var kvp in aliasToConnectionStringMap)
            {
                var name = kvp.Key;
                var connectionString = kvp.Value;

                if (name == QueueAddress.DefaultStorageAccountAlias)
                {
                    throw new ArgumentException("Don't use default empty name for mapping connection strings", nameof(aliasToConnectionStringMap));
                }

                Add(name, connectionString);
            }
        }

        /// <summary>
        /// Maps the account name to a connection string, throwing when no mapping found.
        /// </summary>
        internal ConnectionString Map(string alias)
        {
            ConnectionString connectionString;

            if (aliasToConnectionStringMap.TryGetValue(alias, out connectionString) == false)
            {
                if (useLogicalQueueAddresses == false)
                {
                    return new ConnectionString(alias);
                }

                throw new KeyNotFoundException($"No account was mapped under following name '{alias}'. Please map it using AddStorageAccount method.");
            }

            return connectionString;
        }

        /// <summary>
        /// Transforms all the <see cref="QueueAddress" /> in <see cref="headersToApplyNameMapping" /> to use logical names.
        /// </summary>
        internal void ApplyMappingToAliases(Dictionary<string, string> headers)
        {
            foreach (var header in headersToApplyNameMapping)
            {
                string headerValue;
                if (headers.TryGetValue(header, out headerValue))
                {
                    var address = QueueAddress.Parse(headerValue);
                    string alias;

                    // no mapping if address is default
                    if (address.IsAccountDefault)
                    {
                        continue;
                    }

                    // try map as connection string
                    if (TryMap(new ConnectionString(address.StorageAccount), out alias))
                    {
                        headers[header] = new QueueAddress(address.QueueName, alias).ToString();
                    }
                    else
                    {
                        if (useLogicalQueueAddresses)
                        {
                            // it must be a logical name, try to find it, otherwise throw
                            if (aliasToConnectionStringMap.ContainsKey(address.StorageAccount) == false)
                            {
                                throw new KeyNotFoundException($"No account was mapped under following name '{address.StorageAccount}'. Please map it using AddStorageAccount method.");
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Transforms all the <see cref="QueueAddress" /> in <see cref="headersToApplyNameMapping" /> to connection string
        /// values to maintain backward compatibility.
        /// </summary>
        internal void ApplyMappingOnOutgoingHeaders(Dictionary<string, string> headers, QueueAddress destinationQueue)
        {
            foreach (var header in headersToApplyNameMapping)
            {
                string headerValue;
                if (headers.TryGetValue(header, out headerValue))
                {
                    var address = QueueAddress.Parse(headerValue);

                    var isFullyQualifiedAddress = address.IsAccountDefault == false;
                    if (isFullyQualifiedAddress)
                    {
                        continue;
                    }

                    if (useLogicalQueueAddresses)
                    {
                        var sendingToAnotherAccount = destinationQueue.IsAccountDefault == false;
                        if (sendingToAnotherAccount && address.IsAccountDefault)
                        {
                            headers[header] = new QueueAddress(address.QueueName, defaultConnectionStringAlias).ToString();
                        }
                    }
                    else
                    {
                        var connectionString = Map(address.StorageAccount);
                        headers[header] = new QueueAddress(address.QueueName, connectionString.Value).ToString();
                    }
                }
            }
        }

        bool TryMap(ConnectionString connectionString, out string alias)
        {
            return connectionStringToAliasMap.TryGetValue(connectionString, out alias);
        }

        internal void Add(string name, string connectionString, bool throwOnExistingEntry = true)
        {
            var value = new ConnectionString(connectionString);
            if (throwOnExistingEntry)
            {
                aliasToConnectionStringMap.Add(name, value);
                connectionStringToAliasMap.Add(value, name);
            }
            else
            {
                aliasToConnectionStringMap[name] = value;
                connectionStringToAliasMap[value] = name;
            }
        }

        Dictionary<ConnectionString, string> connectionStringToAliasMap = new Dictionary<ConnectionString, string>();
        Dictionary<string, ConnectionString> aliasToConnectionStringMap = new Dictionary<string, ConnectionString>();

        string defaultConnectionStringAlias;
        bool useLogicalQueueAddresses;

        static string[] headersToApplyNameMapping =
        {
            Headers.ReplyToAddress
        };
    }
}