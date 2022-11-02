namespace NServiceBus.Transport.AzureStorageQueues
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    class NoOpSubscriptionStore : ISubscriptionStore
    {
        static readonly Task<IEnumerable<string>> EmptySubscribers = Task.FromResult(Enumerable.Empty<string>());

        public Task<IEnumerable<string>> GetSubscribers(Type eventType, CancellationToken cancellationToken = default) => EmptySubscribers;
        public Task Subscribe(string endpointName, string endpointAddress, Type eventType, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Unsubscribe(string endpointName, Type eventType, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}