namespace NServiceBus.AzureStorageQueues.PubSub
{
    using System.Threading;
    using System.Threading.Tasks;
    using Extensibility;
    using Transport;
    using Unicast.Messages;

    class NoOpSubscriptionManager : ISubscriptionManager
    {
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