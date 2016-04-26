namespace NServiceBus.AzureStorageQueues
{
    using System;
    using System.Runtime.Serialization;
    using Microsoft.WindowsAzure.Storage.Queue;

    class EnvelopeDeserializationFailed : SerializationException
    {
        public EnvelopeDeserializationFailed(CloudQueueMessage message, Exception ex)
            : base($"Failed to deserialize message envelope for message with id {message.Id}. Make sure the configured serializer is used across all endpoints or configure the message wrapper serializer for this endpoint using the `SerializeMessageWrapperWith` extension on the transport configuration. Please refer to the Azure Storage Queue Transport configuration documentation for more details.", ex)
        {
            FailedMessage = message;
        }

        public CloudQueueMessage FailedMessage { get; }
    }
}