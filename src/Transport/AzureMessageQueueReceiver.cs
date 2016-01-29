namespace NServiceBus.Azure.Transports.WindowsAzureStorageQueues
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Transactions;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Queue;
    using Newtonsoft.Json;
    using NServiceBus.Transports;

    public class AzureMessageQueueReceiver
    {
        public const int DefaultMessageInvisibleTime = 30000;
        public const int DefaultPeekInterval = 50;
        public const int DefaultMaximumWaitTimeWhenIdle = 1000;
        public const int DefaultBatchSize = 10;
        public const bool DefaultPurgeOnStartup = false;
        public const string DefaultConnectionString = "UseDevelopmentStorage=true";
        public const bool DefaultQueuePerInstance = false;

        CloudQueue azureQueue;
        Queue<CloudQueueMessage> batchQueue = new Queue<CloudQueueMessage>();
        CloudQueueClient client;
        JsonSerializer messageSerializer;
        int timeToDelayNextPeek;
        bool useTransactions;

        public AzureMessageQueueReceiver(JsonSerializer messageSerializer, CloudQueueClient client)
        {
            this.messageSerializer = messageSerializer;
            this.client = client;
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

        public void Init(string address, bool transactional)
        {
            useTransactions = transactional;

            var queueName = AzureMessageQueueUtils.GetQueueName(address);

            azureQueue = client.GetQueueReference(queueName);
            azureQueue.CreateIfNotExists();

            if (PurgeOnStartup)
            {
                azureQueue.Clear();
            }
        }

        public async Task<IncomingMessage> Receive(CancellationToken token)
        {
            var rawMessage = await GetMessage(token);

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
                return DeserializeMessage(rawMessage);
            }
            catch (Exception ex)
            {
                throw new EnvelopeDeserializationFailed(rawMessage, ex);
            }
            finally
            {
                if (!useTransactions || Transaction.Current == null)
                {
                    DeleteMessage(rawMessage);
                }
                else
                {
                    Transaction.Current.EnlistVolatile(new ReceiveResourceManager(azureQueue, rawMessage), EnlistmentOptions.None);
                }
            }
        }

        async Task<CloudQueueMessage> GetMessage(CancellationToken token)
        {
            if (batchQueue.Count == 0)
            {
                var messages = await azureQueue.GetMessagesAsync(BatchSize, TimeSpan.FromMilliseconds(MessageInvisibleTime), null, null, token);
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

        void DeleteMessage(CloudQueueMessage message)
        {
            try
            {
                azureQueue.DeleteMessage(message);
            }
            catch (StorageException ex)
            {
                if (ex.RequestInformation.HttpStatusCode != 404)
                {
                    throw;
                }
            }
        }

        IncomingMessage DeserializeMessage(CloudQueueMessage rawMessage)
        {
            MessageWrapper m;
            using (var stream = new MemoryStream(rawMessage.AsBytes))
            {
                try
                {
                    m = messageSerializer.Deserialize<MessageWrapper>(new JsonTextReader(new StreamReader(stream)));
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

            m.Headers[Headers.ReplyToAddress] = m.ReplyToAddress;
            m.Headers[Headers.CorrelationId] = m.CorrelationId;
            m.Headers[Headers.TimeToBeReceived] = m.TimeToBeReceived.ToString();
            m.Headers[Headers.MessageIntent] = m.MessageIntent.ToString(); // message intent exztension method

            return new IncomingMessage(m.Id, m.Headers, new MemoryStream(m.Body));
        }
    }
}