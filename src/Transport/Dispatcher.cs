namespace NServiceBus.Azure.Transports.WindowsAzureStorageQueues
{
    using System;
    using System.Collections.Concurrent;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Storage.Queue;
    using Newtonsoft.Json;
    using NServiceBus.Extensibility;
    using NServiceBus.Logging;
    using NServiceBus.Settings;
    using NServiceBus.Transports;
    using NServiceBus.Unicast.Queuing;

    public class Dispatcher : IDispatchMessages
    {
        static readonly ConcurrentDictionary<string, bool> rememberExistence = new ConcurrentDictionary<string, bool>();
        private static readonly UTF8Encoding NoBomUtf8Encoding = new UTF8Encoding(false);
        readonly ICreateQueueClients createQueueClients;
        readonly string defaultConnectionString;
        readonly ILog logger = LogManager.GetLogger(typeof(Dispatcher));
        readonly JsonSerializer messageSerializer;
        readonly DeterminesBestConnectionStringForStorageQueues validation;

        public Dispatcher(ICreateQueueClients createQueueClients, JsonSerializer messageSerializer, ReadOnlySettings settings
            , string defaultConnectionString)
        {
            this.createQueueClients = createQueueClients;
            this.messageSerializer = messageSerializer;
            this.defaultConnectionString = defaultConnectionString;
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
            await sendQueue.AddMessageAsync(rawMessage, timeToBeReceived, null, null, null).ConfigureAwait(false);
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
                    Headers = headers,
                    MessageIntent = messageIntent
                };

                using (var streamWriter = new StreamWriter(stream, NoBomUtf8Encoding))
                {
                    messageSerializer.Serialize(new JsonTextWriter(streamWriter), toSend);
                    streamWriter.Flush();
                }
                return new CloudQueueMessage(stream.ToArray());
            }
        }
    }
}