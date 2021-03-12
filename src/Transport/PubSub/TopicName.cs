namespace NServiceBus.Transport.AzureStorageQueues
{
    using System;

    static class TopicName
    {
        public static string From(Type messageType) => $"{messageType.FullName}";
    }
}