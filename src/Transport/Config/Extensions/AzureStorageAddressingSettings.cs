namespace NServiceBus
{
    using System;
    using System.Collections.Generic;
    using AzureStorageQueues;
    using AzureStorageQueues.Config;

    class AzureStorageAddressingSettings
    {
        internal void RegisterMapping(string defaultConnectionStringAlias, Dictionary<string, AccountInfo> aliasToConnectionStringMap, bool shouldUseAccountAliases)
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
                var accountInfo = kvp.Value;

                if (name == QueueAddress.DefaultStorageAccountAlias)
                {
                    throw new ArgumentException("Don't use default empty name for mapping connection strings", nameof(aliasToConnectionStringMap));
                }

                Add(accountInfo);
            }
        }

        /// <summary>
        /// Maps the account name to a connection string, throwing when no mapping found.
        /// </summary>
        internal ConnectionString Map(QueueAddress address)
        {
            if (aliasToAccountInfoMap.TryGetValue(address.StorageAccount, out var accountInfo) == false)
            {
                if (useLogicalQueueAddresses == false)
                {
                    return new ConnectionString(address.StorageAccount);
                }

                throw new KeyNotFoundException($"No account was mapped under following name '{address.StorageAccount}'. Please map it using AddStorageAccount method.");
            }

            return accountInfo.Connection;
        }

        /// <summary>
        /// Transforms all the <see cref="QueueAddress" /> in <see cref="headersToApplyNameMapping" /> to use logical names.
        /// </summary>
        internal void ApplyMappingToAliases(Dictionary<string, string> headers)
        {
            foreach (var header in headersToApplyNameMapping)
            {
                if (headers.TryGetValue(header, out var headerValue))
                {
                    var address = QueueAddress.Parse(headerValue);

                    // no mapping if address is default
                    if (address.IsAccountDefault)
                    {
                        continue;
                    }

                    // try map as connection string
                    if (TryMap(new ConnectionString(address.StorageAccount), out var alias))
                    {
                        headers[header] = new QueueAddress(address.QueueName, alias).ToString();
                    }
                    else
                    {
                        if (useLogicalQueueAddresses)
                        {
                            // it must be a logical name, try to find it, otherwise throw
                            if (aliasToAccountInfoMap.ContainsKey(address.StorageAccount) == false)
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
                if (headers.TryGetValue(header, out var headerValue))
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
                        var connectionString = Map(address);
                        headers[header] = new QueueAddress(address.QueueName, connectionString.Value).ToString();
                    }
                }
            }
        }

        bool TryMap(ConnectionString connectionString, out string alias)
        {
            return connectionStringToAliasMap.TryGetValue(connectionString, out alias);
        }

        internal void Add(AccountInfo accountInfo, bool throwOnExistingEntry = true)
        {
            if (throwOnExistingEntry)
            {
                aliasToAccountInfoMap.Add(accountInfo.Alias, accountInfo);
                connectionStringToAliasMap.Add(accountInfo.Connection, accountInfo.Alias);
            }
            else
            {
                aliasToAccountInfoMap[accountInfo.Alias] = accountInfo;
                connectionStringToAliasMap[accountInfo.Connection] = accountInfo.Alias;
            }
        }

        Dictionary<ConnectionString, string> connectionStringToAliasMap = new Dictionary<ConnectionString, string>();
        Dictionary<string, AccountInfo> aliasToAccountInfoMap = new Dictionary<string, AccountInfo>();

        string defaultConnectionStringAlias;
        bool useLogicalQueueAddresses;

        static string[] headersToApplyNameMapping =
        {
            Headers.ReplyToAddress
        };
    }
}