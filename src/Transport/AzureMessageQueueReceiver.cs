namespace NServiceBus.Transport.AzureStorageQueues
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Azure;
    using global::Azure.Storage.Queues;
    using global::Azure.Storage.Queues.Models;
    using Logging;

    class AzureMessageQueueReceiver
    {
        public AzureMessageQueueReceiver(IMessageEnvelopeUnwrapper unwrapper, IQueueServiceClientProvider queueServiceClientProvider, QueueAddressGenerator addressGenerator, MessageWrapperSerializer serializer, bool purgeOnStartup, TimeSpan messageInvisibleTime)
        {
            this.unwrapper = unwrapper;
            queueServiceClient = queueServiceClientProvider.Client;
            this.addressGenerator = addressGenerator;
            this.serializer = serializer;
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

        public async Task Init(string inputQueueAddress, string errorQueueAddress, CancellationToken cancellationToken = default)
        {
            inputQueue = await GetQueue(inputQueueAddress, cancellationToken).ConfigureAwait(false);
            errorQueue = await GetQueue(errorQueueAddress, cancellationToken).ConfigureAwait(false);

            if (PurgeOnStartup)
            {
                await inputQueue.ClearMessagesAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        async Task<QueueClient> GetQueue(string address, CancellationToken cancellationToken)
        {
            var name = addressGenerator.GetQueueName(address);
            var queue = queueServiceClient.GetQueueClient(name);
            await queue.CreateIfNotExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            return queue;
        }

        internal async Task Receive(int batchSize, List<MessageRetrieved> receivedMessages, BackoffStrategy backoffStrategy, CancellationToken cancellationToken = default)
        {
            Logger.DebugFormat("Getting messages from queue with max batch size of {0}", batchSize);
            Response<QueueMessage[]> rawMessagesResponse = await inputQueue.ReceiveMessagesAsync(batchSize, MessageInvisibleTime, cancellationToken).ConfigureAwait(false);

            // https://learn.microsoft.com/en-us/rest/api/storageservices/get-messages#response-headers
            // UTC values and formatted as described in RFC 1123.
            // The RFC1123 pattern reflects a defined standard, and the property is read-only. Therefore, it is always the same, regardless of the culture.
            if ((rawMessagesResponse.GetRawResponse().Headers
                    .TryGetValue("Date", out var serverResponseUtcDateTimeAsString) &&
                DateTimeOffset.TryParseExact(serverResponseUtcDateTimeAsString, "r", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var serverResponseUtcDateTime)) == false)
            {
                serverResponseUtcDateTime = DateTimeOffset.UtcNow;
            }

            foreach (var rawMessage in rawMessagesResponse.Value)
            {
                receivedMessages.Add(new MessageRetrieved(unwrapper, serializer, rawMessage, serverResponseUtcDateTime, inputQueue, errorQueue));
            }

            await backoffStrategy.OnBatch(receivedMessages.Count, cancellationToken).ConfigureAwait(false);
        }

        IMessageEnvelopeUnwrapper unwrapper;

        QueueAddressGenerator addressGenerator;
        MessageWrapperSerializer serializer;
        QueueClient inputQueue;
        QueueClient errorQueue;
        QueueServiceClient queueServiceClient;

        public string QueueName => inputQueue.Name;

        static readonly ILog Logger = LogManager.GetLogger<AzureMessageQueueReceiver>();
    }
}