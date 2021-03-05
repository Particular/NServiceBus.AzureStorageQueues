﻿namespace NServiceBus.Transport.AzureStorageQueues
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Table;

    class SubscriptionStore
    {
        readonly CloudTable subscriptionTable;

        public SubscriptionStore(CloudTable subscriptionTable)
        {
            this.subscriptionTable = subscriptionTable;
        }

        public async Task<IEnumerable<string>> GetSubscribers(Type eventType, CancellationToken cancellationToken)
        {
            var topics = GetTopics(eventType);

            if (topics.Length == 0)
            {
                return Enumerable.Empty<string>();
            }

            var retrieveTasks = new List<Task<IEnumerable<SubscriptionEntity>>>(topics.Length);
            var addresses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var topic in topics)
            {
                retrieveTasks.Add(Retrieve(topic, subscriptionTable, cancellationToken));
            }

            await Task.WhenAll(retrieveTasks).ConfigureAwait(false);

            foreach (var retrieveTask in retrieveTasks)
            {
                var subscriptionEntities = retrieveTask.GetAwaiter().GetResult();
                foreach (var subscriptionEntity in subscriptionEntities)
                {
                    addresses.Add(subscriptionEntity.Address);
                }
            }
            return addresses;
        }

        static async Task<IEnumerable<SubscriptionEntity>> Retrieve(string topic, CloudTable table, CancellationToken cancellationToken)
        {
            var query = new TableQuery<SubscriptionEntity>().Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, topic));
            return await table.ExecuteQueryAsync(query, 1000, cancellationToken).ConfigureAwait(false);
        }

        public Task Subscribe(string endpointName, string endpointAddress, Type eventType, CancellationToken cancellationToken)
        {
            var operation = TableOperation.InsertOrReplace(new SubscriptionEntity
            {
                Topic = TopicName.From(eventType),
                Endpoint = endpointName,
                Address = endpointAddress
            });
            return subscriptionTable.ExecuteAsync(operation, cancellationToken);
        }

        public Task Unsubscribe(string endpointName, Type eventType, CancellationToken cancellationToken)
        {
            var operation = TableOperation.Delete(new SubscriptionEntity
            {
                Topic = TopicName.From(eventType),
                Endpoint = endpointName,
                ETag = "*"
            });

            return subscriptionTable.ExecuteAsync(operation, cancellationToken);
        }

        string[] GetTopics(Type messageType) => eventTypeToTopicListMap.GetOrAdd(messageType, GenerateTopics);

        internal static string[] GenerateTopics(Type messageType) =>
            GenerateMessageHierarchy(messageType)
                .Select(TopicName.From)
                .ToArray();

        static IEnumerable<Type> GenerateMessageHierarchy(Type messageType)
        {
            if (messageType == null)
            {
                yield break;
            }

            var type = messageType;

            while (type != null)
            {
                yield return type;

                type = type.BaseType;
            }

            foreach (var @interface in messageType.GetInterfaces())
            {
                yield return @interface;
            }
        }

        ConcurrentDictionary<Type, string[]> eventTypeToTopicListMap = new ConcurrentDictionary<Type, string[]>();
    }
}