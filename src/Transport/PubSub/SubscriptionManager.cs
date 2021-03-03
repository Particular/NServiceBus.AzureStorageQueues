namespace NServiceBus.Transport.AzureStorageQueues
{
    using System.Threading;
    using System.Threading.Tasks;
    using Extensibility;
    using Microsoft.Azure.Cosmos.Table;
    using Unicast.Messages;

    class SubscriptionManager : ISubscriptionManager
    {
#pragma warning disable IDE0052
        readonly CloudTable subscriptionTable;
#pragma warning restore IDE0052

        public SubscriptionManager(CloudTable subscriptionTable)
        {
            this.subscriptionTable = subscriptionTable;
        }

        public Task SubscribeAll(MessageMetadata[] eventTypes, ContextBag context, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task Unsubscribe(MessageMetadata eventType, ContextBag context, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}