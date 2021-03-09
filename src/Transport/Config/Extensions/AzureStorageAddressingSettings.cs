namespace NServiceBus.Transport.AzureStorageQueues
{
    using System;
    using System.Collections.Generic;
    using global::Azure.Storage.Queues;
    using Microsoft.Azure.Cosmos.Table;

    class AzureStorageAddressingSettings
    {
        public AzureStorageAddressingSettings(QueueAddressGenerator addressGenerator)
        {
            this.addressGenerator = addressGenerator;
        }

        internal void RegisterMapping(string defaultConnectionStringAlias, string defaultSubscriptionTableName, Dictionary<string, AccountInfo> aliasToConnectionStringMap)
        {
            this.defaultConnectionStringAlias = defaultConnectionStringAlias;
            this.defaultSubscriptionTableName = defaultSubscriptionTableName;

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
        internal QueueServiceClient Map(QueueAddress address, MessageIntentEnum messageIntent)
        {
            if (registeredEndpoints.TryGetValue(address.QueueName, out var accountInfo))
            {
                return accountInfo.QueueServiceClient;
            }

            var storageAccountAlias = address.Alias;
            if (aliasToAccountInfoMap.TryGetValue(storageAccountAlias, out accountInfo) == false)
            {
                // If this is a reply message with a connection string use the connection string to construct a queue service client.
                // This was a reply message coming from an older endpoint w/o aliases.
                if (messageIntent == MessageIntentEnum.Reply && CloudStorageAccount.TryParse(address.Alias, out _))
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

        internal void Add(AccountInfo accountInfo, bool throwOnExistingEntry = true)
        {
            if (throwOnExistingEntry)
            {
                aliasToAccountInfoMap.Add(accountInfo.Alias, accountInfo);
            }
            else
            {
                aliasToAccountInfoMap[accountInfo.Alias] = accountInfo;
            }

            foreach (var endpointWithEvents in accountInfo.PublishedEventsByEndpoint)
            {
                var queue = addressGenerator.GetQueueName(endpointWithEvents.Key);
                registeredEndpoints[queue] = accountInfo;

                var (events, subscriptionTableName) = endpointWithEvents.Value;

                foreach (var @event in events)
                {
                    typeToSubscriptionInformation[@event] = (accountInfo, subscriptionTableName);
                }
            }
        }

        internal (string alias, CloudTableClient cloudTableClient, string subscriptionTableName) GetSubscriptionInfo(Type eventType)
        {
            if (typeToSubscriptionInformation.TryGetValue(eventType, out (AccountInfo accountInfo, string subscriptionTableName) found))
            {
                return (found.accountInfo.Alias, found.accountInfo.CloudTableClient, found.subscriptionTableName);
            }

            return (defaultConnectionStringAlias, aliasToAccountInfoMap[defaultConnectionStringAlias].CloudTableClient, defaultSubscriptionTableName);
        }

        QueueAddressGenerator addressGenerator;
        Dictionary<string, AccountInfo> aliasToAccountInfoMap = new Dictionary<string, AccountInfo>();
        Dictionary<string, AccountInfo> registeredEndpoints = new Dictionary<string, AccountInfo>();
        Dictionary<Type, (AccountInfo, string)> typeToSubscriptionInformation = new Dictionary<Type, (AccountInfo, string)>();

        string defaultConnectionStringAlias;
        string defaultSubscriptionTableName;
    }
}