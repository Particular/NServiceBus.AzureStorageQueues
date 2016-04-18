namespace NServiceBus.Azure.Transports.WindowsAzureStorageQueues
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Storage.Queue;

    class AzureMessageQueueReceiver
    {
        QueueAddressGenerator addressGenerator;

        CloudQueue azureQueue;
        CloudQueueClient client;
        int timeToDelayNextPeek;
        private MessageEnvelopUnpacker unpacker;

        public AzureMessageQueueReceiver(MessageEnvelopUnpacker unpacker, CloudQueueClient client, QueueAddressGenerator addressGenerator)
        {
            this.unpacker = unpacker;
            this.client = client;
            this.addressGenerator = addressGenerator;
        }

        /// <summary>
        ///     Sets the amount of time, in milliseconds, to add to the time to wait before checking for a new message
        /// </summary>
        public int PeekInterval { get; set; }

        /// <summary>
        ///     Sets the maximum amount of time, in milliseconds, that the queue will wait before checking for a new message
        /// </summary>
        public int MaximumWaitTimeWhenIdle { get; set; }

        /// <summary>
        ///     Sets whether or not the transport should purge the input
        ///     queue when it is started.
        /// </summary>
        public bool PurgeOnStartup { get; set; }

        /// <summary>
        ///     Controls how long messages should be invisible to other callers when receiving messages from the queue
        /// </summary>
        public int MessageInvisibleTime { get; set; }

        /// <summary>
        ///     Controls the number of messages that will be read in bulk from the queue
        /// </summary>
        public int BatchSize { get; set; }

        public async Task Init(string address)
        {
            var queueName = addressGenerator.GetQueueName(address);

            azureQueue = client.GetQueueReference(queueName);
            await azureQueue.CreateIfNotExistsAsync().ConfigureAwait(false);

            if (PurgeOnStartup)
            {
                await azureQueue.ClearAsync().ConfigureAwait(false);
            }
        }

        internal async Task<IEnumerable<MessageRetrieved>> Receive(CancellationToken token)
        {
            var rawMessages = await azureQueue.GetMessagesAsync(BatchSize, TimeSpan.FromMilliseconds(MessageInvisibleTime), null, null, token).ConfigureAwait(false);

            var messageFound = false;
            var messages = new List<MessageRetrieved>();
            foreach (var rawMessage in rawMessages)
            {
                messageFound = true;
                messages.Add(new MessageRetrieved(unpacker, rawMessage, azureQueue));
            }

            if (!messageFound)
            {
                if (timeToDelayNextPeek + PeekInterval < MaximumWaitTimeWhenIdle)
                {
                    timeToDelayNextPeek += PeekInterval;
                }
                else timeToDelayNextPeek = MaximumWaitTimeWhenIdle;

                await Task.Delay(timeToDelayNextPeek, token).ConfigureAwait(false);
            }

            timeToDelayNextPeek = 0;
            return messages;
        }


    }
}