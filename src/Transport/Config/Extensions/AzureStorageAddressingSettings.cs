﻿namespace NServiceBus
{
    using System;
    using System.Collections.Generic;
    using AzureStorageQueues;
    using AzureStorageQueues.Config;

    sealed class AzureStorageAddressingSettings
    {
        internal void RegisterMapping(string defaultConnectionStringName, Dictionary<string, string> name2connectionString, bool shouldUseAccountNames)
        {
            var hasAnyMapping = name2connectionString != null && name2connectionString.Count > 0;
            if (hasAnyMapping == false)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(defaultConnectionStringName))
            {
                throw new Exception("The mapping of account names instead of connection strings is enabled, but the default connection string name isn\'t provided. Provide the default connection string name when adding more accounts");
            }

            this.defaultConnectionStringName = defaultConnectionStringName;
            useLogicalQueueAddresses = shouldUseAccountNames;

            foreach (var kvp in name2connectionString)
            {
                var name = kvp.Key;
                var connectionString = kvp.Value;

                if (name == QueueAddress.DefaultStorageAccountName)
                {
                    throw new ArgumentException("Don't use default empty name for mapping connection strings", nameof(name2connectionString));
                }

                Add(name, connectionString);
            }
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
        internal void ApplyMappingToLogicalName(Dictionary<string, string> headers)
        {
            foreach (var header in headersToApplyNameMapping)
            {
                string headerValue;
                if (headers.TryGetValue(header, out headerValue))
                {
                    var address = QueueAddress.Parse(headerValue);
                    string name;

                    // no mapping if address is default
                    if (address.IsAccountDefault)
                    {
                        continue;
                    }

                    // try map as connection string
                    if (TryMap(new ConnectionString(address.StorageAccount), out name))
                    {
                        headers[header] = new QueueAddress(address.QueueName, name).ToString();
                    }
                    else
                    {
                        // it must be a logical name, try to find it, otherwise throw
                        if (name2connectionString.ContainsKey(address.StorageAccount) == false)
                        {
                            throw new KeyNotFoundException($"No account was mapped under following name '{address.StorageAccount}'. Please map it using AddStorageAccount method.");
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
                            headers[header] = new QueueAddress(address.QueueName, defaultConnectionStringName).ToString();
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

        bool TryMap(ConnectionString connectionString, out string name)
        {
            return connectionString2name.TryGetValue(connectionString, out name);
        }

        internal void Add(string name, string connectionString, bool throwOnExistingEntry = true)
        {
            var value = new ConnectionString(connectionString);
            if (throwOnExistingEntry)
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

        string defaultConnectionStringName;
        bool useLogicalQueueAddresses;

        static string[] headersToApplyNameMapping =
        {
            Headers.ReplyToAddress
        };
    }
}