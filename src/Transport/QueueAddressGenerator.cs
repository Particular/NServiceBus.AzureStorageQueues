namespace NServiceBus.Transport.AzureStorageQueues
{
    using System;
    using System.Collections.Concurrent;

    class QueueAddressGenerator
    {
        public QueueAddressGenerator(Func<string, string> sanitizer) => this.sanitizer = sanitizer;

        public string GetQueueName(string address)
        {
            var queueName = address.ToLowerInvariant();

            return sanitizedQueueNames.GetOrAdd(queueName, static (name, sanitizer) => sanitizer(name), sanitizer);
        }

        Func<string, string> sanitizer;
        ConcurrentDictionary<string, string> sanitizedQueueNames = new ConcurrentDictionary<string, string>();
    }
}