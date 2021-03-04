namespace NServiceBus.Transport.AzureStorageQueues
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

        public async Task<IEnumerable<string>> GetSubscribers(string endpointName, Type eventType, CancellationToken cancellationToken)
        {
            var topics = GetTopics(eventType);

            if (topics.Length == 0)
            {
                return Enumerable.Empty<string>();
            }

            var retrieveTasks = new List<Task<SubscriptionEntity>>(topics.Length);
            var addresses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var topic in topics)
            {
                retrieveTasks.Add(Retrieve(topic, endpointName, subscriptionTable, cancellationToken));
            }

            await Task.WhenAll(retrieveTasks).ConfigureAwait(false);

            foreach (var retrieveTask in retrieveTasks)
            {
                var subscriptionEntity = retrieveTask.GetAwaiter().GetResult();
                if (subscriptionEntity != null)
                {
                    addresses.Add(subscriptionEntity.Address);
                }
            }
            return addresses;
        }

        static async Task<SubscriptionEntity> Retrieve(string topic, string endpointName, CloudTable table, CancellationToken cancellationToken)
        {
            var result = await table.ExecuteAsync(TableOperation.Retrieve<SubscriptionEntity>(topic, endpointName), cancellationToken).ConfigureAwait(false);
            return (SubscriptionEntity)result.Result;
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

            while (type.UnderlyingSystemType != typeof(object))
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