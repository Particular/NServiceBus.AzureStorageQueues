namespace NServiceBus.AzureStorageQueues
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Storage.Queue;

    class AzureMessageQueueReceiver
    {
        public AzureMessageQueueReceiver(MessageEnvelopeUnwrapper unwrapper, CloudQueueClient client, QueueAddressGenerator addressGenerator)
        {
            this.unwrapper = unwrapper;
            this.client = client;
            this.addressGenerator = addressGenerator;
        }

        /// <summary>
        /// Sets the amount of time, in milliseconds, to add to the time to wait before checking for a new message
        /// </summary>
        public TimeSpan PeekInterval { get; set; }

        /// <summary>
        /// Sets the maximum amount of time that the queue will wait before checking for a new message
        /// </summary>
        public TimeSpan MaximumWaitTimeWhenIdle { get; set; }

        /// <summary>
        /// Sets whether or not the transport should purge the input
        /// queue when it is started.
        /// </summary>
        public bool PurgeOnStartup { get; set; }

        /// <summary>
        /// Controls how long messages should be invisible to other callers when receiving messages from the queue
        /// </summary>
        public TimeSpan MessageInvisibleTime { get; set; }

        /// <summary>
        /// Controls the number of messages that will be read in bulk from the queue
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
            var rawMessages = await azureQueue.GetMessagesAsync(BatchSize, MessageInvisibleTime, null, null, token).ConfigureAwait(false);

            var messageFound = false;
            var messages = new List<MessageRetrieved>();
            foreach (var rawMessage in rawMessages)
            {
                messageFound = true;
                messages.Add(new MessageRetrieved(unwrapper, rawMessage, azureQueue));
            }

            if (!messageFound)
            {
                if (timeToDelayNextPeek + PeekInterval < MaximumWaitTimeWhenIdle)
                {
                    timeToDelayNextPeek += PeekInterval;
                }
                else
                {
                    timeToDelayNextPeek = MaximumWaitTimeWhenIdle;
                }

                await Task.Delay(timeToDelayNextPeek, token).ConfigureAwait(false);
            }

            timeToDelayNextPeek = TimeSpan.Zero;
            return messages;
        }

        MessageEnvelopeUnwrapper unwrapper;

        QueueAddressGenerator addressGenerator;

        CloudQueue azureQueue;
        CloudQueueClient client;
        TimeSpan timeToDelayNextPeek;
    }
}