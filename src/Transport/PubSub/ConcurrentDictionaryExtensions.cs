namespace NServiceBus.Transport.AzureStorageQueues
{
    using System;
    using System.Collections.Concurrent;

    static class ConcurrentDictionaryExtensions
    {
        public static TValue GetOrAdd<TKey, TValue, TArg>(
            this ConcurrentDictionary<TKey, TValue> dictionary,
            TKey key, Func<TKey, TArg, TValue> valueFactory, TArg factoryArgument)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (valueFactory == null)
            {
                throw new ArgumentNullException(nameof(valueFactory));
            }

            while (true)
            {
                if (dictionary.TryGetValue(key, out TValue value))
                {
                    return value;
                }

                value = valueFactory(key, factoryArgument);
                if (dictionary.TryAdd(key, value))
                {
                    return value;
                }
            }
        }
    }
}