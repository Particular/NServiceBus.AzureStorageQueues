namespace NServiceBus.Transport.AzureStorageQueues
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Extensibility;
    using Unicast.Messages;

    class SubscriptionManager : ISubscriptionManager
    {
        readonly ISubscriptionStore subscriptionStore;
        readonly string localAddress;
        readonly string endpointName;

        public SubscriptionManager(ISubscriptionStore subscriptionStore, string endpointName, string localAddress)
        {
            this.endpointName = endpointName;
            this.localAddress = localAddress;
            this.subscriptionStore = subscriptionStore;
        }

        public Task SubscribeAll(MessageMetadata[] eventTypes, ContextBag context, CancellationToken cancellationToken = default)
        {
            var tasks = new List<Task>(eventTypes.Length);
            foreach (var eventType in eventTypes)
            {
                tasks.Add(subscriptionStore.Subscribe(endpointName, localAddress, eventType.MessageType, cancellationToken));
            }
            return Task.WhenAll(tasks);
        }

        public Task Unsubscribe(MessageMetadata eventType, ContextBag context, CancellationToken cancellationToken = default) =>
            subscriptionStore.Unsubscribe(endpointName, eventType.MessageType, cancellationToken);
    }
}