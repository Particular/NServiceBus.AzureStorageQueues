namespace NServiceBus.Transport.AzureStorageQueues
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Azure.Storage.Queues;
    using global::Azure.Storage.Queues.Models;
    using Logging;

    class AzureMessageQueueReceiver
    {
        public AzureMessageQueueReceiver(IMessageEnvelopeUnwrapper unwrapper, IQueueServiceClientProvider queueServiceClientProviderProvider, QueueAddressGenerator addressGenerator, bool purgeOnStartup, TimeSpan messageInvisibleTime)
        {
            this.unwrapper = unwrapper;
            queueServiceClient = queueServiceClientProviderProvider.Client;
            this.addressGenerator = addressGenerator;
            PurgeOnStartup = purgeOnStartup;
            MessageInvisibleTime = messageInvisibleTime;
        }

        /// <summary>
        /// Sets whether or not the transport should purge the input
        /// queue when it is started.
        /// </summary>
        public bool PurgeOnStartup { get; }

        /// <summary>
        /// Controls how long messages should be invisible to other callers when receiving messages from the queue
        /// </summary>
        public TimeSpan MessageInvisibleTime { get; }

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
            var queue = queueServiceClient.GetQueueClient(name);
            await queue.CreateIfNotExistsAsync().ConfigureAwait(false);
            return queue;
        }

        internal async Task Receive(int batchSize, List<MessageRetrieved> receivedMessages, BackoffStrategy backoffStrategy, CancellationToken token)
        {
            Logger.DebugFormat("Getting messages from queue with max batch size of {0}", batchSize);
            QueueMessage[] rawMessages = await inputQueue.ReceiveMessagesAsync(batchSize, MessageInvisibleTime, token).ConfigureAwait(false);

            foreach (var rawMessage in rawMessages)
            {
                receivedMessages.Add(new MessageRetrieved(unwrapper, rawMessage, inputQueue, errorQueue));
            }

            await backoffStrategy.OnBatch(receivedMessages.Count, token).ConfigureAwait(false);
        }

        IMessageEnvelopeUnwrapper unwrapper;

        QueueAddressGenerator addressGenerator;

        QueueClient inputQueue;
        QueueClient errorQueue;
        QueueServiceClient queueServiceClient;

        public string QueueName => inputQueue.Name;

        static readonly ILog Logger = LogManager.GetLogger<AzureMessageQueueReceiver>();
    }
}