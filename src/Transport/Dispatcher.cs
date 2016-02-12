namespace NServiceBus.Azure.Transports.WindowsAzureStorageQueues
{
    using System;
    using System.Collections.Concurrent;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Storage.Queue;
    using NServiceBus.Extensibility;
    using NServiceBus.Logging;
    using NServiceBus.Settings;
    using NServiceBus.Transports;
    using NServiceBus.Unicast.Queuing;

    internal class Dispatcher : IDispatchMessages
    {
        static readonly ConcurrentDictionary<string, bool> rememberExistence = new ConcurrentDictionary<string, bool>();
        readonly QueueAddressGenerator addressGenerator;
        readonly ICreateQueueClients createQueueClients;
        readonly string defaultConnectionString;
        readonly ILog logger = LogManager.GetLogger(typeof(Dispatcher));
        readonly MessageWrapperSerializer messageSerializer;
        readonly DeterminesBestConnectionStringForStorageQueues validation;

        public Dispatcher(ICreateQueueClients createQueueClients, MessageWrapperSerializer messageSerializer, ReadOnlySettings settings
            , string defaultConnectionString, QueueAddressGenerator addressGenerator)
        {
            this.createQueueClients = createQueueClients;
            this.messageSerializer = messageSerializer;
            this.defaultConnectionString = defaultConnectionString;
            this.addressGenerator = addressGenerator;
            validation = new DeterminesBestConnectionStringForStorageQueues(settings, defaultConnectionString);
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
            // The Destination will be just a queue name.
            var queueName = operation.Destination;

            var connectionString = GiveMeConnectionStringForTheQueue(queueName);
            var sendClient = createQueueClients.Create(connectionString);
            var sendQueue = sendClient.GetQueueReference(addressGenerator.GetQueueName(queueName));

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
            try
            {
                await sendQueue.AddMessageAsync(rawMessage, timeToBeReceived, null, null, null).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                throw new UnableToDispatchException(ex)
                {
                    Queue = queueName
                };
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
                var headers = msg.Headers;

                // TODO: investigate the way how the function suppose to work
                var replyToAddress = validation.Determine(headers.GetValueOrDefault(Headers.ReplyToAddress));

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