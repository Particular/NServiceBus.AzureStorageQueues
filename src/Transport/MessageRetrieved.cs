namespace NServiceBus.Azure.Transports.WindowsAzureStorageQueues
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Queue;

    class MessageRetrieved
    {
        static byte[] EmptyContent = new byte[0];
        CloudQueue azureQueue;
        bool handleAckNack;
        CloudQueueMessage rawMessage;

        public MessageRetrieved(MessageWrapper wrapper, CloudQueueMessage rawMessage, CloudQueue azureQueue, bool handleAckNack)
        {
            Wrapper = wrapper;
            this.rawMessage = rawMessage;
            this.azureQueue = azureQueue;
            this.handleAckNack = handleAckNack;
        }

        public MessageWrapper Wrapper { get; }

        /// <summary>
        ///     Acknowledes the successful processing of the message.
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
        ///     Rejects the message requeueing it in the queue.
        /// </summary>
        public async Task Nack()
        {
            if (handleAckNack == false)
            {
                return;
            }

            try
            {
                // the simplest solution to push the message back is to update its visibity timeout to 0 which is ok according to the API:
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
    }
}