namespace NServiceBus.Transport.AzureStorageQueues
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Transports.WindowsAzureStorageQueues;
    using global::Azure.Storage.Queues;
    using Logging;
    using NServiceBus.AzureStorageQueues;
    using Transport;
    using Unicast.Queuing;

    class Dispatcher : IMessageDispatcher
    {
        public Dispatcher(string endpointName, QueueAddressGenerator addressGenerator, AzureStorageAddressingSettings addressing, MessageWrapperSerializer serializer, NativeDelayDeliveryPersistence nativeDelayDeliveryPersistence, SubscriptionStore subscriptionStore)
        {
            this.endpointName = endpointName;
            this.subscriptionStore = subscriptionStore;
            this.addressGenerator = addressGenerator;
            this.addressing = addressing;
            this.serializer = serializer;
            this.nativeDelayDeliveryPersistence = nativeDelayDeliveryPersistence;
        }

        public async Task Dispatch(TransportOperations outgoingMessages, TransportTransaction transaction, CancellationToken token = default)
        {
            int totalNumberOfOperations = outgoingMessages.UnicastTransportOperations.Count + outgoingMessages.MulticastTransportOperations.Count;

            var unicastOperations = new List<UnicastTransportOperation>(totalNumberOfOperations);
            unicastOperations.AddRange(outgoingMessages.UnicastTransportOperations);

            foreach (var multicastTransportOperation in outgoingMessages.MulticastTransportOperations)
            {
                unicastOperations.AddRange(await ConvertTo(multicastTransportOperation, token).ConfigureAwait(false));
            }

            // TODO: Deduplicate by MessageId and Destination
            var sends = new List<Task>(totalNumberOfOperations);
            foreach (var unicastTransportOperation in unicastOperations)
            {
                sends.Add(Send(unicastTransportOperation, CancellationToken.None));
            }

            await Task.WhenAll(sends).ConfigureAwait(false);
        }

        async Task<IEnumerable<UnicastTransportOperation>> ConvertTo(MulticastTransportOperation transportOperation,
            CancellationToken cancellationToken)
        {
            var subscribers =
                await subscriptionStore.GetSubscribers(endpointName, transportOperation.MessageType, cancellationToken)
                    .ConfigureAwait(false);

            return from subscriber in subscribers
                   select new UnicastTransportOperation(
                       transportOperation.Message,
                       subscriber,
                       transportOperation.Properties,
                       transportOperation.RequiredDispatchConsistency
                   );
        }

        public async Task Send(UnicastTransportOperation operation, CancellationToken cancellationToken)
        {
            if (logger.IsDebugEnabled)
            {
                logger.DebugFormat("Sending message (ID: '{0}') to {1}", operation.Message.MessageId, operation.Destination);
            }

            if (NativeDelayDeliveryPersistence.IsDelayedMessage(operation, out var dueDate))
            {
                await nativeDelayDeliveryPersistence.ScheduleAt(operation, dueDate, cancellationToken).ConfigureAwait(false);
                return;
            }

            // The destination might be in a queue@destination format
            var destination = operation.Destination;

            var messageIntent = operation.GetMessageIntent();
            var queueAddress = QueueAddress.Parse(destination, messageIntent == MessageIntentEnum.Reply);
            var queueServiceClient = addressing.Map(queueAddress, messageIntent);

            var queueName = addressGenerator.GetQueueName(queueAddress.QueueName);
            var sendQueue = queueServiceClient.GetQueueClient(queueName);

            if (!await ExistsAsync(sendQueue).ConfigureAwait(false))
            {
                throw new QueueNotFoundException(queueAddress.ToString(), $"Destination queue '{queueAddress}' does not exist. This queue may have to be created manually.", null);
            }

            var toBeReceived = operation.Properties.DiscardIfNotReceivedBefore;
            var timeToBeReceived = toBeReceived != null && toBeReceived.MaxTime < TimeSpan.MaxValue ? toBeReceived.MaxTime : (TimeSpan?)null;

            if (timeToBeReceived.HasValue)
            {
                if (timeToBeReceived.Value == TimeSpan.Zero)
                {
                    var messageType = operation.Message.Headers[Headers.EnclosedMessageTypes].Split(',').First();
                    logger.WarnFormat("TimeToBeReceived is set to zero for message of type '{0}'. Cannot send operation.", messageType);
                    return;
                }

                // user explicitly specified TimeToBeReceived that is not TimeSpan.MaxValue - fail
                if (timeToBeReceived.Value > CloudQueueMessageMaxTimeToLive && timeToBeReceived.Value != TimeSpan.MaxValue)
                {
                    var messageType = operation.Message.Headers[Headers.EnclosedMessageTypes].Split(',').First();
                    throw new InvalidOperationException($"TimeToBeReceived is set to more than 30 days for message type '{messageType}'.");
                }

                var seconds = Convert.ToInt64(Math.Ceiling(timeToBeReceived.Value.TotalSeconds));
                if (seconds <= 0)
                {
                    throw new Exception($"Message cannot be sent with a provided delay of {timeToBeReceived.Value.TotalMilliseconds} ms.");
                }

                timeToBeReceived = TimeSpan.FromSeconds(seconds);
            }

            var wrapper = BuildMessageWrapper(operation, queueAddress);
            await Send(wrapper, sendQueue, timeToBeReceived ?? CloudQueueMessageMaxTimeToLive).ConfigureAwait(false);
        }

        Task Send(MessageWrapper wrapper, QueueClient sendQueue, TimeSpan timeToBeReceived)
        {
            string base64String;

            using (var stream = new MemoryStream())
            {
                serializer.Serialize(wrapper, stream);

                var bytes = stream.ToArray();
                base64String = Convert.ToBase64String(bytes);
            }

            return sendQueue.SendMessageAsync(base64String, timeToLive: timeToBeReceived);
        }

        async Task<bool> ExistsAsync(QueueClient sendQueue)
        {
            var key = sendQueue.Uri.ToString();
            return await rememberExistence.GetOrAdd(key, async keyNotFound =>
             {
                 var exists = await sendQueue.ExistsAsync().ConfigureAwait(false);
                 return exists.Value;
             }).ConfigureAwait(false);
        }

        MessageWrapper BuildMessageWrapper(IOutgoingTransportOperation operation, QueueAddress destinationQueue)
        {
            var msg = operation.Message;
            var headers = new Dictionary<string, string>(msg.Headers);

            addressing.ApplyMappingOnOutgoingHeaders(headers, destinationQueue);

            return new MessageWrapper
            {
                Id = msg.MessageId,
                Body = msg.Body,
                CorrelationId = headers.GetValueOrDefault(Headers.CorrelationId),
                // TODO: Will be addresses in another PR
                // Recoverable = operation.GetDeliveryConstraint<NonDurableDelivery>() == null,
                ReplyToAddress = headers.GetValueOrDefault(Headers.ReplyToAddress),
                Headers = headers,
                MessageIntent = operation.GetMessageIntent()
            };
        }

        readonly MessageWrapperSerializer serializer;
        readonly NativeDelayDeliveryPersistence nativeDelayDeliveryPersistence;

        readonly QueueAddressGenerator addressGenerator;
        readonly AzureStorageAddressingSettings addressing;
        readonly ConcurrentDictionary<string, Task<bool>> rememberExistence = new ConcurrentDictionary<string, Task<bool>>();
        readonly SubscriptionStore subscriptionStore;
        readonly string endpointName;

        static readonly TimeSpan CloudQueueMessageMaxTimeToLive = TimeSpan.FromDays(30);
        static readonly ILog logger = LogManager.GetLogger<Dispatcher>();
    }
}
