﻿namespace NServiceBus.Azure.Transports.WindowsAzureStorageQueues
{
    using System;
    using System.Collections.Concurrent;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Storage.Queue;
    using NServiceBus.Azure.Transports.WindowsAzureStorageQueues.Config;
    using NServiceBus.Extensibility;
    using NServiceBus.Logging;
    using NServiceBus.Transports;
    using NServiceBus.Unicast.Queuing;

    internal class Dispatcher : IDispatchMessages
    {
        static readonly ConcurrentDictionary<string, bool> rememberExistence = new ConcurrentDictionary<string, bool>();
        readonly QueueAddressGenerator addressGenerator;
        readonly AzureStorageAddressingSettings addressing;
        readonly ICreateQueueClients createQueueClients;
        readonly ILog logger = LogManager.GetLogger(typeof(Dispatcher));
        readonly MessageWrapperSerializer messageSerializer;

        public Dispatcher(ICreateQueueClients createQueueClients, MessageWrapperSerializer messageSerializer, QueueAddressGenerator addressGenerator, AzureStorageAddressingSettings addressing)
        {
            this.createQueueClients = createQueueClients;
            this.messageSerializer = messageSerializer;
            this.addressGenerator = addressGenerator;
            this.addressing = addressing;
        }

        public async Task Dispatch(TransportOperations outgoingMessages, ContextBag context)
        {
            if (outgoingMessages.MulticastTransportOperations.Any())
            {
                throw new Exception("The Azure Storage Queue transport only supports unicast transport operations.");
            }

            foreach (var unicastTransportOperation in outgoingMessages.UnicastTransportOperations)
            {
                await Send(unicastTransportOperation).ConfigureAwait(false);
            }
        }

        private async Task Send(UnicastTransportOperation operation)
        {
            // The destination might be in a queue@destination format
            var destination = operation.Destination;

            var queue = QueueAtAccount.Parse(destination);
            var connectionString = GetConnectionString(queue);

            var sendClient = createQueueClients.Create(connectionString);
            var q = addressGenerator.GetQueueName(queue.QueueName);
            var sendQueue = sendClient.GetQueueReference(q);

            if (!Exists(sendQueue))
            {
                throw new QueueNotFoundException
                {
                    Queue = destination
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
            try
            {
                await sendQueue.AddMessageAsync(rawMessage, timeToBeReceived, null, null, null).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                throw new UnableToDispatchException(ex)
                {
                    Queue = queue.QueueName
                };
            }
        }

        private string GetConnectionString(QueueAtAccount destination)
        {
            string connectionString;
            if (addressing.TryMapAccount(destination.StorageAccount, out connectionString) == false)
            {
                connectionString = destination.StorageAccount;
            }
            return connectionString;
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
                var headers = msg.Headers;

                string replyToAddress;
                if (headers.TryGetValue(Headers.ReplyToAddress, out replyToAddress))
                {
                    var q = QueueAtAccount.Parse(replyToAddress);
                    var connectionString = GetConnectionString(q);
                    replyToAddress = new QueueAtAccount(q.QueueName, connectionString).ToString();
                }

                var messageIntent = default(MessageIntentEnum);
                string messageIntentString;
                if (headers.TryGetValue(Headers.MessageIntent, out messageIntentString))
                {
                    Enum.TryParse(messageIntentString, true, out messageIntent);
                }

                var toSend = new MessageWrapper
                {
                    Id = msg.MessageId,
                    Body = msg.Body,
                    CorrelationId = headers.GetValueOrDefault(Headers.CorrelationId),
                    Recoverable = operation.GetDeliveryConstraint<NonDurableDelivery>() == null,
                    ReplyToAddress = replyToAddress,
                    TimeToBeReceived = timeToBeReceived ?? TimeSpan.MaxValue,
                    Headers = new HeadersCollection(headers),
                    MessageIntent = messageIntent
                };

                messageSerializer.Serialize(toSend, stream);
                return new CloudQueueMessage(stream.ToArray());
            }
        }
    }
}