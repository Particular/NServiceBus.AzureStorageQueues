namespace NServiceBus.Transport.AzureStorageQueues
{
    using System.Collections.Generic;

    static class HashSetExtensions
    {
        public static void AddRange<T>(this HashSet<T> hashSet, IEnumerable<T> range)
        {
            foreach (var item in range)
            {
                hashSet.Add(item);
            }
        }
    }
}