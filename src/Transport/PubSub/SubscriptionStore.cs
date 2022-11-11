namespace NServiceBus.Transport.AzureStorageQueues
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Azure;
    using global::Azure.Data.Tables;

    class SubscriptionStore : ISubscriptionStore
    {
        readonly AzureStorageAddressingSettings storageAddressingSettings;

        public SubscriptionStore(AzureStorageAddressingSettings storageAddressingSettings) =>
            this.storageAddressingSettings = storageAddressingSettings;

        public async Task<IEnumerable<string>> GetSubscribers(Type eventType, CancellationToken cancellationToken = default)
        {
            var topics = GetTopics(eventType);

            if (topics.Length == 0)
            {
                return Enumerable.Empty<string>();
            }

            var retrieveTasks = new List<Task<IEnumerable<string>>>(topics.Length);
            var addresses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            (_, TableClient tableClient) = storageAddressingSettings.GetSubscriptionTableClient(eventType);

            foreach (var topic in topics)
            {
                retrieveTasks.Add(RetrieveAddresses(topic, tableClient, cancellationToken));
            }

            var addressResults = await Task.WhenAll(retrieveTasks).ConfigureAwait(false);
            foreach (var addressResult in addressResults)
            {
                addresses.AddRange(addressResult);
            }

            return addresses;
        }

        static async Task<IEnumerable<string>> RetrieveAddresses(string topic, TableClient tableClient, CancellationToken cancellationToken) =>
            await tableClient
                .QueryAsync<SubscriptionEntity>(e => e.Topic == topic, select: SelectedSubscriptionColumns, cancellationToken: cancellationToken)
                .Select(e => e.Address)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

        public Task Subscribe(string endpointName, string endpointAddress, Type eventType, CancellationToken cancellationToken = default)
        {
            (string alias, TableClient tableClient) = storageAddressingSettings.GetSubscriptionTableClient(eventType);
            var address = new QueueAddress(endpointAddress, alias);
            var entity = new SubscriptionEntity
            {
                Topic = TopicName.From(eventType),
                Endpoint = endpointName,
                Address = address.ToString()
            };

            return tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken: cancellationToken);
        }

        public Task Unsubscribe(string endpointName, Type eventType, CancellationToken cancellationToken = default)
        {
            (_, TableClient tableClient) = storageAddressingSettings.GetSubscriptionTableClient(eventType);
            return tableClient.DeleteEntityAsync(
                TopicName.From(eventType),
                endpointName,
                new ETag("*"),
                cancellationToken);
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

        readonly ConcurrentDictionary<Type, string[]> eventTypeToTopicListMap = new();

        static readonly IEnumerable<string> SelectedSubscriptionColumns = new[] { nameof(SubscriptionEntity.Address) };
    }
}