namespace NServiceBus.Azure.Transports.WindowsAzureStorageQueues
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Storage.Queue;

    class AzureMessageQueueReceiver
    {
        public AzureMessageQueueReceiver(MessageWrapperSerializer messageSerializer, CloudQueueClient client, QueueAddressGenerator addressGenerator)
        {
            this.messageSerializer = messageSerializer;
            this.client = client;
            this.addressGenerator = addressGenerator;
        }

        /// <summary>
        /// Sets the amount of time, in milliseconds, to add to the time to wait before checking for a new message
        /// </summary>
        public int PeekInterval { get; set; }

        /// <summary>
        /// Sets the maximum amount of time, in milliseconds, that the queue will wait before checking for a new message
        /// </summary>
        public int MaximumWaitTimeWhenIdle { get; set; }

        /// <summary>
        /// Sets whether or not the transport should purge the input
        /// queue when it is started.
        /// </summary>
        public bool PurgeOnStartup { get; set; }

        /// <summary>
        /// Controls how long messages should be invisible to other callers when receiving messages from the queue
        /// </summary>
        public int MessageInvisibleTime { get; set; }

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
            var rawMessages = await azureQueue.GetMessagesAsync(BatchSize, TimeSpan.FromMilliseconds(MessageInvisibleTime), null, null, token).ConfigureAwait(false);

            bool messageFound = false;
            var messages = new List<MessageRetrieved>();
            foreach (var rawMessage in rawMessages)
            {
                messageFound = true;
                try
                {
                    var wrapper = DeserializeMessage(rawMessage);
                    messages.Add(new MessageRetrieved(wrapper, rawMessage, azureQueue, true));
                }
                catch (Exception ex)
                {
                    throw new EnvelopeDeserializationFailed(rawMessage, ex);
                }
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

        QueueAddressGenerator addressGenerator;

        CloudQueue azureQueue;
        Queue<CloudQueueMessage> batchQueue = new Queue<CloudQueueMessage>();
        CloudQueueClient client;
        MessageWrapperSerializer messageSerializer;
        int timeToDelayNextPeek;
    }
}