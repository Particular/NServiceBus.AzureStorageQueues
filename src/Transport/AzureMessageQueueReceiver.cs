namespace NServiceBus.AzureStorageQueues
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Queue;

    class AzureMessageQueueReceiver
    {
        public AzureMessageQueueReceiver(MessageEnvelopeUnwrapper unwrapper, CloudQueueClient client, QueueAddressGenerator addressGenerator, BackoffStrategy backoffStrategy)
        {
            this.unwrapper = unwrapper;
            this.client = client;
            this.addressGenerator = addressGenerator;
            this.backoffStrategy = backoffStrategy;
        }

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

        public async Task Init(string inputQueueAddress, string errorQueueAddress)
        {
            inputQueue = await GetQueue(inputQueueAddress).ConfigureAwait(false);
            errorQueue = await GetQueue(errorQueueAddress).ConfigureAwait(false);

            if (PurgeOnStartup)
            {
                await inputQueue.ClearAsync().ConfigureAwait(false);
            }
        }

        async Task<CloudQueue> GetQueue(string address)
        {
            var name = addressGenerator.GetQueueName(address);
            var queue = client.GetQueueReference(name);
            await queue.CreateIfNotExistsAsync().ConfigureAwait(false);
            return queue;
        }

        internal async Task<List<MessageRetrieved>> Receive(CancellationToken token)
        {
            var rawMessages = await inputQueue.GetMessagesAsync(BatchSize, MessageInvisibleTime, new QueueRequestOptions { MaximumExecutionTime = TimeSpan.FromSeconds(5), ServerTimeout = TimeSpan.FromSeconds(5)}, new OperationContext(), token).ConfigureAwait(false);

            var messageFound = false;
            List<MessageRetrieved> messages = null;
            foreach (var rawMessage in rawMessages)
            {
                if (!messageFound)
                {
                    messages = new List<MessageRetrieved>(BatchSize);
                    messageFound = true;
                }

                messages.Add(new MessageRetrieved(unwrapper, rawMessage, inputQueue, errorQueue));
            }

            await backoffStrategy.OnBatch(BatchSize, messageFound ? messages.Count : 0, token).ConfigureAwait(false);
            return messageFound ? messages : noMessagesFound;
        }

        MessageEnvelopeUnwrapper unwrapper;

        QueueAddressGenerator addressGenerator;

        CloudQueue inputQueue;
        CloudQueue errorQueue;
        CloudQueueClient client;
        readonly BackoffStrategy backoffStrategy;

        public string QueueName => inputQueue.Name;

        static List<MessageRetrieved> noMessagesFound = new List<MessageRetrieved>();
    }
}