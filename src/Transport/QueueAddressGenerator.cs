namespace NServiceBus.AzureStorageQueues
{
    using System;
    using System.Collections.Concurrent;

    class QueueAddressGenerator
    {
        public QueueAddressGenerator(Func<string, string> sanitizer)
        {
            this.sanitizer = sanitizer;
        }

        public string GetQueueName(string address)
        {
            var queueName = address.ToLowerInvariant();

            return sanitizedQueueNames.GetOrAdd(queueName, name => sanitizer(name));
        }

        Func<string, string> sanitizer;
        ConcurrentDictionary<string, string> sanitizedQueueNames = new ConcurrentDictionary<string, string>();
    }
}