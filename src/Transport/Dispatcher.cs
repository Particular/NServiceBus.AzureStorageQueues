namespace NServiceBus.AzureStorageQueues
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Transports.WindowsAzureStorageQueues;
    using Config;
    using Extensibility;
    using Logging;
    using Microsoft.WindowsAzure.Storage.Queue;
    using Transport;
    using Unicast.Queuing;

    class Dispatcher : IDispatchMessages
    {
        public Dispatcher(QueueAddressGenerator addressGenerator, AzureStorageAddressingSettings addressing, MessageWrapperSerializer serializer, Func<UnicastTransportOperation, CancellationToken, Task<bool>> shouldSend)
        {
            createQueueClients = new CreateQueueClients();
            this.addressGenerator = addressGenerator;
            this.addressing = addressing;
            this.serializer = serializer;
            this.shouldSend = shouldSend;
        }

        public Task Dispatch(TransportOperations outgoingMessages, TransportTransaction transaction, ContextBag context)
        {
            if (outgoingMessages.MulticastTransportOperations.Any())
            {
                throw new Exception("The Azure Storage Queue transport only supports unicast transport operations.");
            }

            var sends = new List<Task>(outgoingMessages.UnicastTransportOperations.Count);
            foreach (var unicastTransportOperation in outgoingMessages.UnicastTransportOperations)
            {
                sends.Add(Send(unicastTransportOperation, CancellationToken.None));
            }

            return Task.WhenAll(sends);
        }

        public async Task Send(UnicastTransportOperation operation, CancellationToken cancellationToken)
        {
            var dispatchDecision = await shouldSend(operation, cancellationToken).ConfigureAwait(false);
            if (dispatchDecision == false)
            {
                return;
            }

            // The destination might be in a queue@destination format
            var destination = operation.Destination;

            var queue = QueueAddress.Parse(destination);
            var connectionString = addressing.Map(queue);

            var sendClient = createQueueClients.Create(connectionString);
            var q = addressGenerator.GetQueueName(queue.QueueName);
            var sendQueue = sendClient.GetQueueReference(q);

            if (!await ExistsAsync(sendQueue).ConfigureAwait(false))
            {
                throw new QueueNotFoundException
                {
                    Queue = queue.ToString()
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
            if (timeToBeReceived != null && timeToBeReceived.Value > CloudQueueMessageMaxTimeToLive && timeToBeReceived != TimeSpan.MaxValue)
            {
                var messageType = operation.Message.Headers[Headers.EnclosedMessageTypes].Split(',').First();
                throw new InvalidOperationException($"TimeToBeReceived is set to more than 30 days for message type '{messageType}'.");
            }

            if (timeToBeReceived.HasValue)
            {
                var seconds = Convert.ToInt64(Math.Ceiling(timeToBeReceived.Value.TotalSeconds));

                if (seconds <= 0)
                {
                    throw new Exception($"Message cannot be sent with a provided delay of {timeToBeReceived.Value.TotalMilliseconds} ms.");
                }

                timeToBeReceived = TimeSpan.FromSeconds(seconds);
            }

            var wrapper = BuildMessageWrapper(operation, queue);
            await Send(wrapper, sendQueue, timeToBeReceived ?? CloudQueueMessageMaxTimeToLive).ConfigureAwait(false);
        }

        Task Send(MessageWrapper wrapper, CloudQueue sendQueue, TimeSpan timeToBeReceived)
        {
            CloudQueueMessage rawMessage;
            using (var stream = new MemoryStream())
            {
                serializer.Serialize(wrapper, stream);
#if NET452
                rawMessage = new CloudQueueMessage(stream.ToArray());
#else
                rawMessage = CloudQueueMessage.CreateCloudQueueMessageFromByteArray(stream.ToArray());
#endif
            }

            return sendQueue.AddMessageAsync(rawMessage, timeToBeReceived, null, null, null);
        }

        Task<bool> ExistsAsync(CloudQueue sendQueue)
        {
            var key = sendQueue.Uri.ToString();
            return rememberExistence.GetOrAdd(key, keyNotFound => sendQueue.ExistsAsync());
        }

        MessageWrapper BuildMessageWrapper(IOutgoingTransportOperation operation, QueueAddress destinationQueue)
        {
            var msg = operation.Message;
            var headers = new Dictionary<string, string>(msg.Headers);
            addressing.ApplyMappingOnOutgoingHeaders(headers, destinationQueue);

            var messageIntent = default(MessageIntentEnum);
            if (headers.TryGetValue(Headers.MessageIntent, out var messageIntentString))
            {
                Enum.TryParse(messageIntentString, true, out messageIntent);
            }

            return new MessageWrapper
            {
                Id = msg.MessageId,
                Body = msg.Body,
                CorrelationId = headers.GetValueOrDefault(Headers.CorrelationId),
                Recoverable = operation.GetDeliveryConstraint<NonDurableDelivery>() == null,
                ReplyToAddress = headers.GetValueOrDefault(Headers.ReplyToAddress),
                Headers = headers,
                MessageIntent = messageIntent
            };
        }

        readonly CreateQueueClients createQueueClients;
        readonly MessageWrapperSerializer serializer;
        readonly Func<UnicastTransportOperation, CancellationToken, Task<bool>> shouldSend;

        readonly QueueAddressGenerator addressGenerator;
        readonly AzureStorageAddressingSettings addressing;
        readonly ConcurrentDictionary<string, Task<bool>> rememberExistence = new ConcurrentDictionary<string, Task<bool>>();

        static readonly TimeSpan CloudQueueMessageMaxTimeToLive = TimeSpan.FromDays(30);
        static readonly ILog logger = LogManager.GetLogger<Dispatcher>();
    }
}