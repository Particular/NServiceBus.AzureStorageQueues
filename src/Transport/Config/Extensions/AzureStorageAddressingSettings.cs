﻿namespace NServiceBus.Transport.AzureStorageQueues
{
    using System;
    using System.Collections.Generic;
    using global::Azure.Data.Tables;
    using global::Azure.Storage.Queues;

    class AzureStorageAddressingSettings
    {
        public AzureStorageAddressingSettings(QueueAddressGenerator addressGenerator, string defaultConnectionStringAlias, string subscriptionTableName, Dictionary<string, AccountInfo> aliasToConnectionStringMap, AccountInfo localAccountInfo)
        {
            this.addressGenerator = addressGenerator;
            this.defaultConnectionStringAlias = defaultConnectionStringAlias;

            Initialize(subscriptionTableName, aliasToConnectionStringMap, localAccountInfo);
        }

        void Initialize(string subscriptionTableName, Dictionary<string, AccountInfo> aliasToConnectionStringMap, AccountInfo localAccountInfo)
        {
            aliasToAccountInfoMap.Add(localAccountInfo.Alias, localAccountInfo);
            typeToSubscriptionInformation.Add(typeof(DefaultLocalEventTypeMatch), (localAccountInfo, subscriptionTableName));

            var hasAnyMapping = aliasToConnectionStringMap != null && aliasToConnectionStringMap.Count > 0;
            if (hasAnyMapping == false)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(defaultConnectionStringAlias))
            {
                throw new Exception("The mapping of storage accounts connection strings to aliases is enforced but the the alias for the default connection string isn\'t provided. Provide the default connection string alias when using more than a single storage account.");
            }

            foreach (var kvp in aliasToConnectionStringMap)
            {
                var name = kvp.Key;
                var accountInfo = kvp.Value;

                if (name == string.Empty)
                {
                    throw new ArgumentException("Don't use empty string as the default connection string alias.", nameof(aliasToConnectionStringMap));
                }

                Add(accountInfo);
            }
        }

        /// <summary>
        /// Maps the account name to a QueueServiceClient, throwing when no mapping found.
        /// </summary>
        internal QueueServiceClient Map(QueueAddress address, MessageIntent messageIntent)
        {
            if (registeredEndpoints.TryGetValue(address, out var accountInfo))
            {
                return accountInfo.QueueServiceClient;
            }

            var storageAccountAlias = address.Alias;
            if (aliasToAccountInfoMap.TryGetValue(storageAccountAlias, out accountInfo) == false)
            {
                // If this is a reply message with a connection string use the connection string to construct a queue service client.
                // This was a reply message coming from an older endpoint w/o aliases.
                if (messageIntent == MessageIntent.Reply && address.Alias.IsValidAzureConnectionString())
                {
                    return new QueueServiceClient(address.Alias);
                }

                throw new Exception($"No account was mapped under following name '{address.Alias}'. Please map it using .AccountRouting().AddAccount() method.");
            }

            return accountInfo.QueueServiceClient;
        }

        /// <summary>
        /// Transforms reply-to header to connection string values to maintain backward compatibility.
        /// </summary>
        internal void ApplyMappingOnOutgoingHeaders(Dictionary<string, string> headers, QueueAddress destinationQueue)
        {
            if (headers.TryGetValue(Headers.ReplyToAddress, out var headerValue))
            {
                var address = QueueAddress.Parse(headerValue);

                if (address.HasNoAlias == false)
                {
                    return;
                }

                var destinationHasAlias = destinationQueue.HasNoAlias == false;
                if (destinationHasAlias && address.HasNoAlias)
                {
                    headers[Headers.ReplyToAddress] = new QueueAddress(address.QueueName, defaultConnectionStringAlias).ToString();
                }
            }
        }

        internal void Add(AccountInfo accountInfo)
        {
            aliasToAccountInfoMap.Add(accountInfo.Alias, accountInfo);

            foreach (var endpointWithEvents in accountInfo.PublishedEventsByEndpoint)
            {
                var queueAddress = new QueueAddress(addressGenerator.GetQueueName(endpointWithEvents.Key), accountInfo.Alias);
                registeredEndpoints[queueAddress] = accountInfo;

                var (events, tableName) = endpointWithEvents.Value;

                foreach (var @event in events)
                {
                    typeToSubscriptionInformation[@event] = (accountInfo, tableName);
                }
            }
        }

        internal (string alias, TableClient tableClient) GetSubscriptionTableClient(Type eventType)
        {
            TableClient subscriptionTableClient;
            if (typeToSubscriptionInformation.TryGetValue(eventType, out (AccountInfo accountInfo, string tableName) found))
            {
                subscriptionTableClient = found.accountInfo.TableServiceClient.GetTableClient(found.tableName);
                return (defaultConnectionStringAlias, subscriptionTableClient);
            }

            (AccountInfo accountInfo, string tableName) = typeToSubscriptionInformation[typeof(DefaultLocalEventTypeMatch)];
            subscriptionTableClient = accountInfo.TableServiceClient.GetTableClient(tableName);
            return (accountInfo.Alias, subscriptionTableClient);
        }

        readonly QueueAddressGenerator addressGenerator;
        readonly Dictionary<string, AccountInfo> aliasToAccountInfoMap = [];
        readonly Dictionary<QueueAddress, AccountInfo> registeredEndpoints = [];
        readonly Dictionary<Type, (AccountInfo, string)> typeToSubscriptionInformation = [];
        readonly string defaultConnectionStringAlias;

        // kind of a wildcard for the local account info
        sealed class DefaultLocalEventTypeMatch
        {
        }
    }
}