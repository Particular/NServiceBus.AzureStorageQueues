namespace NServiceBus.Transport.AzureStorageQueues
{
    using System;
    using System.Threading.Tasks;
    using Extensibility;

    class SubscriptionManager : IManageSubscriptions
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

        public Task Subscribe(Type eventType, ContextBag context) => subscriptionStore.Subscribe(endpointName, localAddress, eventType);

        public Task Unsubscribe(Type eventType, ContextBag context) => subscriptionStore.Unsubscribe(endpointName, eventType);
    }
}