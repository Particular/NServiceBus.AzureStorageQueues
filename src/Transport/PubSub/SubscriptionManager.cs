namespace NServiceBus.Transport.AzureStorageQueues
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Extensibility;
    using Microsoft.Azure.Cosmos.Table;
    using Unicast.Messages;

    class SubscriptionManager : ISubscriptionManager
    {
        readonly CloudTable subscriptionTable;
        readonly string localAddress;

        public SubscriptionManager(CloudTable subscriptionTable, string localAddress)
        {
            this.subscriptionTable = subscriptionTable;
            this.localAddress = localAddress;
        }

        public async Task SubscribeAll(MessageMetadata[] eventTypes, ContextBag context, CancellationToken cancellationToken)
        {
            try
            {
                var tasks = eventTypes.Select(eventMetadata => CreateTopics(eventMetadata.MessageHierarchy, cancellationToken));

                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Gracefully finish
            }
        }

        async Task CreateTopics(Type[] types, CancellationToken cancellationToken)
        {
            foreach (var type in types)
            {
                var operation = TableOperation.InsertOrReplace(new TableEntity(partitionKey: type.FullName, rowKey: localAddress));

                await subscriptionTable.ExecuteAsync(operation, cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task Unsubscribe(MessageMetadata eventType, ContextBag context, CancellationToken cancellationToken)
        {
            try
            {
                await DeleteTopics(eventType.MessageHierarchy, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Gracefully finish
            }
        }

        async Task DeleteTopics(Type[] types, CancellationToken cancellationToken)
        {
            foreach (var type in types)
            {
                var operation = TableOperation.Delete(new TableEntity(partitionKey: type.FullName, rowKey: localAddress) { ETag = "*" });

                await subscriptionTable.ExecuteAsync(operation, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}