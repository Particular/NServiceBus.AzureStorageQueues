namespace NServiceBus.Transport.AzureStorageQueues
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;


    class CachedSubscriptionStore : ISubscriptionStore
    {
        public CachedSubscriptionStore(ISubscriptionStore inner, TimeSpan cacheFor)
        {
            this.inner = inner;
            this.cacheFor = cacheFor;
        }

        public Task<IEnumerable<string>> GetSubscribers(Type eventType, CancellationToken cancellationToken = default)
        {
            var cacheItem = Cache.GetOrAdd(eventType,
                (Type type, (ISubscriptionStore store, CancellationToken token) args) => new CachedSubscriptions
                {
                    StoredUtc = DateTime.UtcNow,
                    Subscribers = args.store.GetSubscribers(type, args.token)
                }, (inner, cancellationToken));

            var age = DateTime.UtcNow - cacheItem.StoredUtc;
            if (age >= cacheFor)
            {
                cacheItem.Subscribers = inner.GetSubscribers(eventType, cancellationToken);
                cacheItem.StoredUtc = DateTime.UtcNow;
            }
            return cacheItem.Subscribers;
        }

        public async Task Subscribe(string endpointName, string endpointAddress, Type eventType, CancellationToken cancellationToken = default)
        {
            await inner.Subscribe(endpointName, endpointAddress, eventType, cancellationToken).ConfigureAwait(false);
            ClearForMessageType(eventType);
        }

        public async Task Unsubscribe(string endpointName, Type eventType, CancellationToken cancellationToken = default)
        {
            await inner.Unsubscribe(endpointName, eventType, cancellationToken).ConfigureAwait(false);
            ClearForMessageType(eventType);
        }

        void ClearForMessageType(Type eventType) => Cache.TryRemove(eventType, out _);

        TimeSpan cacheFor;
        ISubscriptionStore inner;
        ConcurrentDictionary<Type, CachedSubscriptions> Cache = new ConcurrentDictionary<Type, CachedSubscriptions>();

        sealed class CachedSubscriptions
        {
            public DateTime StoredUtc { get; set; } // Internal usage, only set/get using private
            public Task<IEnumerable<string>> Subscribers { get; set; }
        }
    }
}