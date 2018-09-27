namespace NServiceBus.Transports.AzureStorageQueues
{
    using System.Collections.Generic;

    static class DictionaryExtensions
    {
        public static TValue GetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, TValue defaultValue = default(TValue))
        {
            return dictionary.TryGetValue(key, out var value) ? value : defaultValue;
        }
    }
}