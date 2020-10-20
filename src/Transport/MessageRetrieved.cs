namespace NServiceBus.Transport.AzureStorageQueues
{
    using System;
    using System.Runtime.Serialization;
    using System.Threading.Tasks;
    using Azure.Transports.WindowsAzureStorageQueues;
    using global::Azure;
    using global::Azure.Storage.Queues;
    using global::Azure.Storage.Queues.Models;
    using Logging;

    class MessageRetrieved
    {
        public MessageRetrieved(IMessageEnvelopeUnwrapper unwrapper, QueueMessage rawMessage, QueueClient inputQueue, QueueClient errorQueue)
        {
            this.unwrapper = unwrapper;
            this.errorQueue = errorQueue;
            this.rawMessage = rawMessage;
            this.inputQueue = inputQueue;
        }

        public long DequeueCount => rawMessage.DequeueCount;

        /// <summary>
        /// Unwraps the raw message body.
        /// </summary>
        /// <exception cref="SerializationException">Thrown when the raw message could not be unwrapped. The raw message is automatically moved to the error queue before this exception is thrown.</exception>
        /// <returns>The actual message wrapper.</returns>
        public async Task<MessageWrapper> Unwrap()
        {
            try
            {
                Logger.DebugFormat("Unwrapping message with native ID: '{0}'", rawMessage.MessageId);
                return unwrapper.Unwrap(rawMessage);
            }
            catch (Exception ex)
            {
                // When a CloudQueueMessage is retrieved and is en-queued directly, message's ID and PopReceipt are mutated.
                // To be able to delete the original message, original message ID and PopReceipt have to be stored aside.
                var messageId = rawMessage.MessageId;
                var messagePopReceipt = rawMessage.PopReceipt;

                await errorQueue.SendMessageAsync(rawMessage.MessageText).ConfigureAwait(false);
                // TODO: might not need this as the new SDK doesn't send a message by using the original message. Rather, copies the text only.
                await inputQueue.DeleteMessageAsync(messageId, messagePopReceipt).ConfigureAwait(false);

                throw new SerializationException($"Failed to deserialize message envelope for message with id {messageId}. Make sure the configured serializer is used across all endpoints or configure the message wrapper serializer for this endpoint using the `SerializeMessageWrapperWith` extension on the transport configuration. Please refer to the Azure Storage Queue Transport configuration documentation for more details.", ex);
            }
        }

        /// <summary>
        /// Acknowledges the successful processing of the message.
        /// </summary>
        public Task Ack()
        {
            AssertVisibilityTimeout();

            return inputQueue.DeleteMessageAsync(rawMessage.MessageId, rawMessage.PopReceipt);
        }

        void AssertVisibilityTimeout()
        {
            if (rawMessage.NextVisibleOn != null)
            {
                var visibleIn = rawMessage.NextVisibleOn.Value - DateTimeOffset.Now;
                if (visibleIn < TimeSpan.Zero)
                {
                    var visibilityTimeoutExceededBy = -visibleIn;
                    throw new LeaseTimeoutException(rawMessage, visibilityTimeoutExceededBy);
                }
            }
        }

        /// <summary>
        /// Rejects the message requeueing it in the queue.
        /// </summary>
        public async Task Nack()
        {
            AssertVisibilityTimeout();

            try
            {
                // the simplest solution to push the message back is to update its visibility timeout to 0 which is ok according to the API:
                // https://msdn.microsoft.com/en-us/library/azure/hh452234.aspx
                await inputQueue.UpdateMessageAsync(rawMessage.MessageId, rawMessage.PopReceipt, visibilityTimeout: TimeSpan.Zero).ConfigureAwait(false);
            }
            catch (RequestFailedException ex) when (ex.ErrorCode != QueueErrorCode.MessageNotFound)
            {
                throw;
            }
        }

        readonly QueueClient inputQueue;
        readonly QueueMessage rawMessage;
        readonly QueueClient errorQueue;
        readonly IMessageEnvelopeUnwrapper unwrapper;

        static ILog Logger = LogManager.GetLogger<MessageRetrieved>();
    }

    class LeaseTimeoutException : Exception
    {
        public LeaseTimeoutException(QueueMessage rawMessage, TimeSpan visibilityTimeoutExceededBy) : base($"The pop receipt of the cloud queue message '{rawMessage.MessageId}' is invalid as it exceeded the next visible time by '{visibilityTimeoutExceededBy}'.")
        {
        }
    }
}
