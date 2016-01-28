﻿namespace NServiceBus.Azure.Transports.WindowsAzureStorageQueues
{
    using System;
    using System.Collections.Concurrent;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Transactions;
    using Microsoft.WindowsAzure.Storage.Queue;
    using NServiceBus.Extensibility;
    using NServiceBus.Logging;
    using NServiceBus.Serialization;
    using NServiceBus.Settings;
    using NServiceBus.Transports;
    using NServiceBus.Unicast.Queuing;

    public class AzureMessageQueueSender : IDispatchMessages
    {
        static readonly ConcurrentDictionary<string, bool> rememberExistence = new ConcurrentDictionary<string, bool>();
        readonly ICreateQueueClients createQueueClients;
        readonly string defaultConnectionString;
        readonly ILog logger = LogManager.GetLogger(typeof(AzureMessageQueueSender));
        readonly IMessageSerializer messageSerializer;
        readonly bool transactionsEnabled;
        readonly DeterminesBestConnectionStringForStorageQueues validation;

        public AzureMessageQueueSender(ICreateQueueClients createQueueClients, IMessageSerializer messageSerializer, ReadOnlySettings settings
            , string defaultConnectionString)
        {
            this.createQueueClients = createQueueClients;
            this.messageSerializer = messageSerializer;
            this.defaultConnectionString = defaultConnectionString;
            validation = new DeterminesBestConnectionStringForStorageQueues(settings, defaultConnectionString);

            // TODO: should we drop this? According to the notes for v6 it's core responsibility now, right?
            transactionsEnabled = settings.GetOrDefault<bool>("Transactions.Enabled");
        }

        public async Task Dispatch(TransportOperations outgoingMessages, ContextBag context)
        {
            if (outgoingMessages.MulticastTransportOperations.Any())
            {
                throw new Exception("The Azure Storage Queue transport only supports unicast transport operations.");
            }

            foreach (var unicastTransportOperation in outgoingMessages.UnicastTransportOperations)
            {
                await Send(unicastTransportOperation);
            }
        }

        private async Task Send(UnicastTransportOperation operation)
        {
            // The Destination will be just a queue name.
            var queueName = operation.Destination;

            var connectionString = GiveMeConnectionStringForTheQueue(queueName);
            var sendClient = createQueueClients.Create(connectionString);
            var sendQueue = sendClient.GetQueueReference(AzureMessageQueueUtils.GetQueueName(queueName));

            if (!Exists(sendQueue))
            {
                throw new QueueNotFoundException
                {
                    Queue = queueName
                };
            }

            var toBeReceived = operation.GetTimeToBeReceived();
            var timeToBeReceived = toBeReceived.HasValue && toBeReceived.Value < TimeSpan.MaxValue ? toBeReceived : null;

            if (timeToBeReceived != null && timeToBeReceived.Value == TimeSpan.Zero)
            {
                var messageType = operation.Message.Headers[Headers.EnclosedMessageTypes].Split(',').First();
                logger.WarnFormat("TimeToBeReceived is set to zero for message of type '{0}'. Cannot send operation.", messageType);
                return;
            }

            // user explicitly specified TimeToBeReceived that is not TimeSpan.MaxValue - fail
            if (timeToBeReceived != null && timeToBeReceived.Value > CloudQueueMessage.MaxTimeToLive && timeToBeReceived != TimeSpan.MaxValue)
            {
                var messageType = operation.Message.Headers[Headers.EnclosedMessageTypes].Split(',').First();
                throw new InvalidOperationException($"TimeToBeReceived is set to more than 7 days (maximum for Azure Storage queue) for message type '{messageType}'.");
            }

            // TimeToBeReceived was not specified on message - go for maximum set by SDK
            if (timeToBeReceived == TimeSpan.MaxValue)
            {
                timeToBeReceived = null;
            }

            var rawMessage = SerializeMessage(operation, timeToBeReceived);

            if (!transactionsEnabled || Transaction.Current == null)
            {
                await sendQueue.AddMessageAsync(rawMessage, timeToBeReceived, null, null, null);
            }
            else
            {
                Transaction.Current.EnlistVolatile(new SendResourceManager(sendQueue, rawMessage, timeToBeReceived), EnlistmentOptions.None);
            }
        }

        // TODO: consider providing a more advanced mapping, providing ability to host queues in different storage accounts, without explicit sending the message to another one
        // This could improve throughput enabling to a separate account for high-volume queueus. Additionally, one could come up with idea sharding the queue across different accounts.
        // This could be done with a modified message pump pulling from different queues.
        private string GiveMeConnectionStringForTheQueue(string queueName)
        {
            return defaultConnectionString;
        }

        bool Exists(CloudQueue sendQueue)
        {
            var key = sendQueue.Uri.ToString();
            return rememberExistence.GetOrAdd(key, keyNotFound => sendQueue.Exists());
        }

        CloudQueueMessage SerializeMessage(IOutgoingTransportOperation operation, TimeSpan? timeToBeReceived)
        {
            using (var stream = new MemoryStream())
            {
                var msg = operation.Message;

                // TODO: validate assumptions
                //var replyToAddress = validation.Determine(config.Settings, message.ReplyToAddress ?? options.ReplyToAddress ?? config.LocalAddress, config.TransportConnectionString());
                var replyToAddress = validation.Determine(msg.Headers[Headers.ReplyToAddress]);

                var messageIntent = default(MessageIntentEnum);
                string messageIntentString;
                if (msg.Headers.TryGetValue(Headers.MessageIntent, out messageIntentString))
                {
                    Enum.TryParse(messageIntentString, true, out messageIntent);
                }

                var toSend = new MessageWrapper
                {
                    Id = msg.MessageId,
                    Body = msg.Body,
                    CorrelationId = msg.Headers[Headers.CorrelationId],
                    Recoverable = operation.GetDeliveryConstraint<NonDurableDelivery>() == null,
                    ReplyToAddress = replyToAddress,
                    TimeToBeReceived = timeToBeReceived ?? TimeSpan.MaxValue,
                    Headers = msg.Headers,
                    MessageIntent = messageIntent
                };

                messageSerializer.Serialize(toSend, stream);
                return new CloudQueueMessage(stream.ToArray());
            }
        }
    }
}