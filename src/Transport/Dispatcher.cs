namespace NServiceBus.Azure.Transports.WindowsAzureStorageQueues
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

    class Dispatcher : IDispatchMessages
    {
        static ConcurrentDictionary<string, bool> rememberExistence = new ConcurrentDictionary<string, bool>();
        QueueAddressGenerator addressGenerator;
        AzureStorageAddressingSettings addressing;
        CreateQueueClients createQueueClients;
        ILog logger = LogManager.GetLogger(typeof(Dispatcher));
        MessageWrapperSerializer messageSerializer;

        public Dispatcher(CreateQueueClients createQueueClients, MessageWrapperSerializer messageSerializer, QueueAddressGenerator addressGenerator, AzureStorageAddressingSettings addressing)
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

        async Task Send(UnicastTransportOperation operation)
        {
            // The destination might be in a queue@destination format
            var destination = operation.Destination;

            var queue = QueueAddress.Parse(destination);
            var connectionString = addressing.Map(queue.StorageAccount);

            var sendClient = createQueueClients.Create(connectionString);
            var q = addressGenerator.GetQueueName(queue.QueueName);
            var sendQueue = sendClient.GetQueueReference(q);

            if (!Exists(sendQueue))
            {
                throw new QueueNotFoundException
                {
                    Queue = queue.ToString(),
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
                var headers = new HeadersCollection(msg.Headers);
                addressing.ApplyMappingOnOutgoingHeaders(headers);

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
                    ReplyToAddress = headers.GetValueOrDefault(Headers.ReplyToAddress),
                    TimeToBeReceived = timeToBeReceived ?? TimeSpan.MaxValue,
                    Headers = headers,
                    MessageIntent = messageIntent
                };

                messageSerializer.Serialize(toSend, stream);
                return new CloudQueueMessage(stream.ToArray());
            }
        }
    }
}