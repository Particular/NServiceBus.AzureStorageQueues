namespace NServiceBus.Transport.AzureStorageQueues
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Azure.Storage.Queues;
    using Logging;

    class AzureMessageQueueReceiver
    {
        public AzureMessageQueueReceiver(IMessageEnvelopeUnwrapper unwrapper, QueueServiceClient service, QueueAddressGenerator addressGenerator)
        {
            this.unwrapper = unwrapper;
            this.service = service;
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
                await inputQueue.ClearMessagesAsync().ConfigureAwait(false);
            }
        }

        async Task<QueueClient> GetQueue(string address)
        {
            var name = addressGenerator.GetQueueName(address);
            var queue = service.GetQueueClient(name);
            await queue.CreateIfNotExistsAsync().ConfigureAwait(false);
            return queue;
        }

        internal async Task Receive(int batchSize, List<MessageRetrieved> receivedMessages, BackoffStrategy backoffStrategy, CancellationToken token)
        {
            Logger.DebugFormat("Getting messages from queue with max batch size of {0}", batchSize);
            var rawMessages = await inputQueue.ReceiveMessagesAsync(batchSize, MessageInvisibleTime, token).ConfigureAwait(false);

            foreach (var rawMessage in rawMessages.Value)
            {
                receivedMessages.Add(new MessageRetrieved(unwrapper, rawMessage, inputQueue, errorQueue));
            }

            await backoffStrategy.OnBatch(receivedMessages.Count, token).ConfigureAwait(false);
        }

        IMessageEnvelopeUnwrapper unwrapper;

        QueueAddressGenerator addressGenerator;

        QueueClient inputQueue;
        QueueClient errorQueue;
        QueueServiceClient service;

        public string QueueName => inputQueue.Name;

        static readonly ILog Logger = LogManager.GetLogger<AzureMessageQueueReceiver>();
    }
}