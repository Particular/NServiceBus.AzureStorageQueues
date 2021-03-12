namespace NServiceBus.Transport.AzureStorageQueues
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading.Tasks;


    class CachedSubscriptionStore : ISubscriptionStore
    {
        public CachedSubscriptionStore(ISubscriptionStore inner, TimeSpan cacheFor)
        {
            this.inner = inner;
            this.cacheFor = cacheFor;
        }

        public Task<IEnumerable<string>> GetSubscribers(Type eventType)
        {
            var cacheItem = cache.GetOrAdd(eventType,
                (type, store) => new CachedSubscriptions
                {
                    StoredUtc = DateTime.UtcNow,
                    Subscribers = store.GetSubscribers(type)
                }, inner);

            var age = DateTime.UtcNow - cacheItem.StoredUtc;
            if (age >= cacheFor)
            {
                cacheItem.Subscribers = inner.GetSubscribers(eventType);
                cacheItem.StoredUtc = DateTime.UtcNow;
            }
            return cacheItem.Subscribers;
        }

        public async Task Subscribe(string endpointName, string endpointAddress, Type eventType)
        {
            await inner.Subscribe(endpointName, endpointAddress, eventType).ConfigureAwait(false);
            ClearForMessageType(eventType);
        }

        public async Task Unsubscribe(string endpointName, Type eventType)
        {
            await inner.Unsubscribe(endpointName, eventType).ConfigureAwait(false);
            ClearForMessageType(eventType);
        }

        void ClearForMessageType(Type eventType) => cache.TryRemove(eventType, out _);

        TimeSpan cacheFor;
        ISubscriptionStore inner;
        ConcurrentDictionary<Type, CachedSubscriptions> cache = new ConcurrentDictionary<Type, CachedSubscriptions>();

        sealed class CachedSubscriptions
        {
            public DateTime StoredUtc { get; set; } // Internal usage, only set/get using private
            public Task<IEnumerable<string>> Subscribers { get; set; }
        }
    }
}