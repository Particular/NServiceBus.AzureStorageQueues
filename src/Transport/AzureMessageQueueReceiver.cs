namespace NServiceBus.Azure.Transports.WindowsAzureStorageQueues
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Storage.Queue;

    internal class AzureMessageQueueReceiver
    {
        public const int DefaultMessageInvisibleTime = 30000;
        public const int DefaultPeekInterval = 50;
        public const int DefaultMaximumWaitTimeWhenIdle = 1000;
        public const int DefaultBatchSize = 10;
        public const bool DefaultPurgeOnStartup = false;
        public const string DefaultConnectionString = "UseDevelopmentStorage=true";
        public const bool DefaultQueuePerInstance = false;
        readonly QueueAddressGenerator addressGenerator;

        CloudQueue azureQueue;
        Queue<CloudQueueMessage> batchQueue = new Queue<CloudQueueMessage>();
        CloudQueueClient client;
        MessageWrapperSerializer messageSerializer;
        int timeToDelayNextPeek;

        public AzureMessageQueueReceiver(MessageWrapperSerializer messageSerializer, CloudQueueClient client, QueueAddressGenerator addressGenerator)
        {
            this.messageSerializer = messageSerializer;
            this.client = client;
            this.addressGenerator = addressGenerator;
            MessageInvisibleTime = DefaultMessageInvisibleTime;
            PeekInterval = DefaultPeekInterval;
            MaximumWaitTimeWhenIdle = DefaultMaximumWaitTimeWhenIdle;
            BatchSize = DefaultBatchSize;
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

        internal async Task<MessageRetrieved> Receive(CancellationToken token)
        {
            var rawMessage = await GetMessage(token).ConfigureAwait(false);

            if (rawMessage == null)
            {
                if (timeToDelayNextPeek < MaximumWaitTimeWhenIdle)
                {
                    timeToDelayNextPeek += PeekInterval;
                }

                await Task.Delay(timeToDelayNextPeek, token).ConfigureAwait(false);
                return null;
            }

            timeToDelayNextPeek = 0;
            try
            {
                var wrapper = DeserializeMessage(rawMessage);
                return new MessageRetrieved(wrapper, rawMessage, azureQueue, true);
            }
            catch (Exception ex)
            {
                throw new EnvelopeDeserializationFailed(rawMessage, ex);
            }
        }

        async Task<CloudQueueMessage> GetMessage(CancellationToken token)
        {
            if (batchQueue.Count == 0)
            {
                var messages = await azureQueue.GetMessagesAsync(BatchSize, TimeSpan.FromMilliseconds(MessageInvisibleTime), null, null, token).ConfigureAwait(false);
                foreach (var receivedMessage in messages)
                {
                    batchQueue.Enqueue(receivedMessage);
                }
            }
            if (batchQueue.Count > 0)
            {
                return batchQueue.Dequeue();
            }

            return null;
        }

        MessageWrapper DeserializeMessage(CloudQueueMessage rawMessage)
        {
            MessageWrapper m;
            using (var stream = new MemoryStream(rawMessage.AsBytes))
            {
                try
                {
                    m = messageSerializer.Deserialize(stream);
                }
                catch (Exception)
                {
                    throw new SerializationException("Failed to deserialize message with id: " + rawMessage.Id);
                }
            }

            if (m == null)
            {
                throw new SerializationException("Failed to deserialize message with id: " + rawMessage.Id);
            }

            if (m.ReplyToAddress != null)
            {
                m.Headers[Headers.ReplyToAddress] = m.ReplyToAddress;
            }
            m.Headers[Headers.CorrelationId] = m.CorrelationId;

            if (m.TimeToBeReceived != TimeSpan.MaxValue)
            {
                m.Headers[Headers.TimeToBeReceived] = m.TimeToBeReceived.ToString();
            }
            m.Headers[Headers.MessageIntent] = m.MessageIntent.ToString(); // message intent exztension method

            return m;
        }
    }
}