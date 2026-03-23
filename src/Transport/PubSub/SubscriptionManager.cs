namespace NServiceBus.Transport.AzureStorageQueues
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Extensibility;
    using Unicast.Messages;

    class SubscriptionManager : ISubscriptionManager
    {
        readonly ISubscriptionStore subscriptionStore;
        readonly string localAddress;
        readonly string endpointName;
        readonly StartupDiagnosticEntries startupDiagnostic;

        public SubscriptionManager(ISubscriptionStore subscriptionStore, string endpointName, string localAddress, StartupDiagnosticEntries startupDiagnostic)
        {
            this.endpointName = endpointName;
            this.localAddress = localAddress;
            this.subscriptionStore = subscriptionStore;
            this.startupDiagnostic = startupDiagnostic;
        }

        public Task SubscribeAll(MessageMetadata[] eventTypes, ContextBag context, CancellationToken cancellationToken = default)
        {
            startupDiagnostic.Add("Manifest-Subscriptions", eventTypes
                .Select(eventType => eventType.MessageType.FullName)
                .ToArray());

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