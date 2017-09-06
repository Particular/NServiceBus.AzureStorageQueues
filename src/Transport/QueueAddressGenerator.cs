namespace NServiceBus.AzureStorageQueues
{
    using System;
    using System.Collections.Concurrent;
    using Config;
    using Settings;

    class QueueAddressGenerator
    {
        public QueueAddressGenerator(ReadOnlySettings settings)
        {
            sanitizer = settings.GetOrDefault<Func<string, string>>(WellKnownConfigurationKeys.Sanitizer);
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