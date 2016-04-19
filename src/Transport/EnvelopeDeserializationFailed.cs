namespace NServiceBus.AzureStorageQueues
{
    using System;
    using System.Runtime.Serialization;
    using Microsoft.WindowsAzure.Storage.Queue;

    class EnvelopeDeserializationFailed : SerializationException
    {
        public EnvelopeDeserializationFailed(CloudQueueMessage message, Exception ex)
            : base("Failed to deserialize message envelope", ex)
        {
            FailedMessage = message;
        }

        public CloudQueueMessage FailedMessage { get; }
    }
}