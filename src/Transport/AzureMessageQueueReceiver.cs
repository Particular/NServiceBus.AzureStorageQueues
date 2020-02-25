namespace NServiceBus.Transport.AzureStorageQueues
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Storage.Queue;
    using Logging;

    class AzureMessageQueueReceiver
    {
        public AzureMessageQueueReceiver(IMessageEnvelopeUnwrapper unwrapper, CloudQueueClient client, QueueAddressGenerator addressGenerator)
        {
            this.unwrapper = unwrapper;
            this.client = client;
            this.addressGenerator = addressGenerator;
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

        internal async Task Receive(int batchSize, List<MessageRetrieved> receivedMessages, BackoffStrategy backoffStrategy, CancellationToken token)
        {
            Logger.DebugFormat("Getting messages from queue with max batch size of {0}", batchSize);
            var rawMessages = await inputQueue.GetMessagesAsync(batchSize, MessageInvisibleTime, null, null, token).ConfigureAwait(false);

            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var rawMessage in rawMessages)
            {
                receivedMessages.Add(new MessageRetrieved(unwrapper, rawMessage, inputQueue, errorQueue));
            }

            await backoffStrategy.OnBatch(receivedMessages.Count, token).ConfigureAwait(false);
        }

        IMessageEnvelopeUnwrapper unwrapper;

        QueueAddressGenerator addressGenerator;

        CloudQueue inputQueue;
        CloudQueue errorQueue;
        CloudQueueClient client;

        public string QueueName => inputQueue.Name;

        static readonly ILog Logger = LogManager.GetLogger<AzureMessageQueueReceiver>();
    }
}