namespace NServiceBus.Transport.AzureStorageQueues
{
    using System;
    using System.Collections.Generic;
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

        public Task<List<string>> GetSubscribers(Type eventType)
        {
            return Task.FromResult(new List<string> { eventType.FullName });
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
    }
}