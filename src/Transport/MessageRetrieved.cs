namespace NServiceBus.AzureStorageQueues
{
    using System;
    using System.Threading.Tasks;
    using Azure.Transports.WindowsAzureStorageQueues;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Queue;

    class MessageRetrieved
    {
        public MessageRetrieved(MessageWrapper wrapper, CloudQueueMessage rawMessage, CloudQueue azureQueue)
        {
            Wrapper = wrapper;
            this.rawMessage = rawMessage;
            this.azureQueue = azureQueue;
        }

        public int DequeueCount => rawMessage.DequeueCount;

        /// <summary>
        /// Acknowledges the successful processing of the message.
        /// </summary>
        public Task Ack()
        {
            AssertVisibilityTimeout();

            return azureQueue.DeleteMessageAsync(rawMessage);
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

        public readonly MessageWrapper Wrapper;
        readonly CloudQueue azureQueue;
        readonly CloudQueueMessage rawMessage;
    }

    class LeaseTimeoutException : Exception
    {
        public LeaseTimeoutException(CloudQueueMessage rawMessage, TimeSpan visibilityTimeoutExceededBy) : base($"The pop receipt of the cloud queue message '{rawMessage.Id}' is invalid as it exceeded the next visible time by '{visibilityTimeoutExceededBy}'.")
        {
        }
    }
}