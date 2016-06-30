namespace NServiceBus.AzureStorageQueues
{
    using System;
    using System.Runtime.Serialization;
    using System.Threading.Tasks;
    using Azure.Transports.WindowsAzureStorageQueues;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Queue;

    class MessageRetrieved
    {
        public MessageRetrieved(MessageEnvelopeUnwrapper unpacker, CloudQueueMessage rawMessage, CloudQueue azureQueue, bool handleAckNack = true)
        {
            this.unpacker = unpacker;
            this.rawMessage = rawMessage;
            this.azureQueue = azureQueue;
            this.handleAckNack = handleAckNack;
        }

        /// <summary>
        /// Unwraps the raw message.
        /// </summary>
        /// <returns>Returns the message wrapper.</returns>
        public MessageWrapper Unwrap()
        {
            try
            {
                return unpacker.Unwrap(rawMessage);
            }
            catch (Exception ex)
            {
                throw new SerializationException($"Failed to deserialize message envelope for message with id {rawMessage.Id}. Make sure the configured serializer is used across all endpoints or configure the message wrapper serializer for this endpoint using the `SerializeMessageWrapperWith` extension on the transport configuration. Please refer to the Azure Storage Queue Transport configuration documentation for more details.", ex);
            }
        }

        /// <summary>
        /// Acknowledges the successful processing of the message.
        /// </summary>
        public async Task Ack()
        {
            if (handleAckNack == false)
            {
                return;
            }

            try
            {
                await azureQueue.DeleteMessageAsync(rawMessage).ConfigureAwait(false);
            }
            catch (StorageException ex)
            {
                if (ex.RequestInformation.HttpStatusCode != 404)
                {
                    throw;
                }
            }
        }

        /// <summary>
        /// Rejects the message requeueing it in the queue.
        /// </summary>
        public async Task Nack()
        {
            if (handleAckNack == false)
            {
                return;
            }

            try
            {
                // the simplest solution to push the message back is to update its visibility timeout to 0 which is ok according to the API:
                // https://msdn.microsoft.com/en-us/library/azure/hh452234.aspx
                rawMessage.SetMessageContent(EmptyContent);
                await azureQueue.UpdateMessageAsync(rawMessage, TimeSpan.Zero, MessageUpdateFields.Visibility).ConfigureAwait(false);
            }
            catch (StorageException ex)
            {
                if (ex.RequestInformation.HttpStatusCode != 404)
                {
                    throw;
                }
            }
        }

        MessageEnvelopeUnwrapper unpacker;

        CloudQueue azureQueue;
        bool handleAckNack;
        CloudQueueMessage rawMessage;
        static byte[] EmptyContent = new byte[0];
    }
}