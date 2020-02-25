namespace NServiceBus.Transport.AzureStorageQueues
{
    using System;
    using System.Runtime.Serialization;
    using System.Threading.Tasks;
    using Azure.Transports.WindowsAzureStorageQueues;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Queue;
    using Logging;

    class MessageRetrieved
    {
        public MessageRetrieved(IMessageEnvelopeUnwrapper unwrapper, CloudQueueMessage rawMessage, CloudQueue inputQueue, CloudQueue errorQueue)
        {
            this.unwrapper = unwrapper;
            this.errorQueue = errorQueue;
            this.rawMessage = rawMessage;
            this.inputQueue = inputQueue;
        }

        public int DequeueCount => rawMessage.DequeueCount;

        /// <summary>
        /// Unwraps the raw message body.
        /// </summary>
        /// <exception cref="SerializationException">Thrown when the raw message could not be unwrapped. The raw message is automatically moved to the error queue before this exception is thrown.</exception>
        /// <returns>The actual message wrapper.</returns>
        public async Task<MessageWrapper> Unwrap()
        {
            try
            {
                Logger.DebugFormat("Unwrappinbg message ID: '{0}'", rawMessage.Id);
                return unwrapper.Unwrap(rawMessage);
            }
            catch (Exception ex)
            {
                await errorQueue.AddMessageAsync(rawMessage).ConfigureAwait(false);
                await inputQueue.DeleteMessageAsync(rawMessage).ConfigureAwait(false);

                throw new SerializationException($"Failed to deserialize message envelope for message with id {rawMessage.Id}. Make sure the configured serializer is used across all endpoints or configure the message wrapper serializer for this endpoint using the `SerializeMessageWrapperWith` extension on the transport configuration. Please refer to the Azure Storage Queue Transport configuration documentation for more details.", ex);
            }
        }

        /// <summary>
        /// Acknowledges the successful processing of the message.
        /// </summary>
        public Task Ack()
        {
            AssertVisibilityTimeout();

            return inputQueue.DeleteMessageAsync(rawMessage);
        }

        void AssertVisibilityTimeout()
        {
            if (rawMessage.NextVisibleTime != null)
            {
                var visibleIn = rawMessage.NextVisibleTime.Value - DateTimeOffset.Now;
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
                await inputQueue.UpdateMessageAsync(rawMessage, TimeSpan.Zero, MessageUpdateFields.Visibility).ConfigureAwait(false);
            }
            catch (StorageException ex)
            {
                if (ex.RequestInformation.HttpStatusCode != 404)
                {
                    throw;
                }
            }
        }

        readonly CloudQueue inputQueue;
        readonly CloudQueueMessage rawMessage;
        readonly CloudQueue errorQueue;
        readonly IMessageEnvelopeUnwrapper unwrapper;

        static ILog Logger = LogManager.GetLogger<MessageRetrieved>();
    }

    class LeaseTimeoutException : Exception
    {
        public LeaseTimeoutException(CloudQueueMessage rawMessage, TimeSpan visibilityTimeoutExceededBy) : base($"The pop receipt of the cloud queue message '{rawMessage.Id}' is invalid as it exceeded the next visible time by '{visibilityTimeoutExceededBy}'.")
        {
        }
    }
}