namespace NServiceBus.Transport.AzureStorageQueues
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    class NoOpSubscriptionStore : ISubscriptionStore
    {
        public Task<IEnumerable<string>> GetSubscribers(Type eventType, CancellationToken cancellationToken) => Task.FromResult(Enumerable.Empty<string>());
        public Task Subscribe(string endpointName, string endpointAddress, Type eventType, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task Unsubscribe(string endpointName, Type eventType, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}