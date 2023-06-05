namespace NServiceBus.Transport.AzureStorageQueues
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Transports.WindowsAzureStorageQueues;
    using DelayedDelivery;
    using DeliveryConstraints;
    using Extensibility;
    using global::Azure.Storage.Queues;
    using Logging;
    using NServiceBus.AzureStorageQueues;
    using NServiceBus.Transport.AzureStorageQueues.Utils;
    using Performance.TimeToBeReceived;
    using Transport;
    using Unicast.Queuing;

    class Dispatcher : IDispatchMessages
    {
        public Dispatcher(QueueAddressGenerator addressGenerator, AzureStorageAddressingSettings addressing, MessageWrapperSerializer serializer, Func<UnicastTransportOperation, DateTimeOffset, CancellationToken, Task> scheduleMessageDelivery, ISubscriptionStore subscriptionStore)
        {
            this.subscriptionStore = subscriptionStore;
            this.addressGenerator = addressGenerator;
            this.addressing = addressing;
            this.serializer = serializer;
            this.scheduleMessageDelivery = scheduleMessageDelivery;
        }

        public async Task Dispatch(TransportOperations outgoingMessages, TransportTransaction transaction, ContextBag context)
        {
            int totalNumberOfOperations = outgoingMessages.UnicastTransportOperations.Count + outgoingMessages.MulticastTransportOperations.Count;

#if NETFRAMEWORK
            var unicastOperations = new HashSet<UnicastTransportOperation>(totalNumberOfOperations, MessageIdAndDestinationEqualityComparer.Instance);
#endif
#if NETSTANDARD
            var unicastOperations = new HashSet<UnicastTransportOperation>(MessageIdAndDestinationEqualityComparer.Instance);
#endif
            unicastOperations.AddRange(outgoingMessages.UnicastTransportOperations);

            foreach (var multicastTransportOperation in outgoingMessages.MulticastTransportOperations)
            {
                unicastOperations.AddRange(await ConvertTo(multicastTransportOperation).ConfigureAwait(false));
            }

            var sends = new List<Task>(totalNumberOfOperations);
            foreach (var unicastTransportOperation in unicastOperations)
            {
                sends.Add(Send(unicastTransportOperation, CancellationToken.None));
            }

            await Task.WhenAll(sends).ConfigureAwait(false);
        }

        async Task<IEnumerable<UnicastTransportOperation>> ConvertTo(MulticastTransportOperation transportOperation)
        {
            var subscribers =
                await subscriptionStore.GetSubscribers(transportOperation.MessageType)
                    .ConfigureAwait(false);

            return from subscriber in subscribers
                   select new UnicastTransportOperation(
                       transportOperation.Message,
                       subscriber,
                       transportOperation.RequiredDispatchConsistency,
                       transportOperation.DeliveryConstraints
                   );
        }

        public async Task Send(UnicastTransportOperation operation, CancellationToken cancellationToken)
        {
            if (Logger.IsDebugEnabled)
            {
                Logger.DebugFormat("Sending message (ID: '{0}') to {1}", operation.Message.MessageId, operation.Destination);
            }

            var delay = GetVisibilityDelay(operation.DeliveryConstraints);
            if (delay != null)
            {
                if (FirstOrDefault<DiscardIfNotReceivedBefore>(operation.DeliveryConstraints) != null)
                {
                    throw new Exception($"Postponed delivery of messages with TimeToBeReceived set is not supported. Remove the TimeToBeReceived attribute to postpone messages of type '{operation.Message.Headers[Headers.EnclosedMessageTypes]}'.");
                }

                await scheduleMessageDelivery(operation, DateTimeOffset.UtcNow + delay.Value, cancellationToken).ConfigureAwait(false);
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

            var toBeReceived = operation.GetTimeToBeReceived();
            var timeToBeReceived = toBeReceived.HasValue && toBeReceived.Value < TimeSpan.MaxValue ? toBeReceived : null;

            if (timeToBeReceived != null && timeToBeReceived.Value == TimeSpan.Zero)
            {
                var messageType = operation.Message.Headers[Headers.EnclosedMessageTypes].Split(',').First();
                Logger.WarnFormat("TimeToBeReceived is set to zero for message of type '{0}'. Cannot send operation.", messageType);
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

            var wrapper = BuildMessageWrapper(operation, queueAddress);
            await Send(wrapper, sendQueue, timeToBeReceived ?? CloudQueueMessageMaxTimeToLive).ConfigureAwait(false);
        }

        internal static TimeSpan? GetVisibilityDelay(List<DeliveryConstraint> constraints)
        {
            var doNotDeliverBefore = FirstOrDefault<DoNotDeliverBefore>(constraints);
            if (doNotDeliverBefore != null)
            {
                return ToNullIfNegative(doNotDeliverBefore.At - DateTimeOffset.UtcNow);
            }

            var delay = FirstOrDefault<DelayDeliveryWith>(constraints);
            if (delay != null)
            {
                return ToNullIfNegative(delay.Delay);
            }

            return null;
        }

        static TDeliveryConstraint FirstOrDefault<TDeliveryConstraint>(List<DeliveryConstraint> constraints)
            where TDeliveryConstraint : DeliveryConstraint
        {
            if (constraints == null || constraints.Count == 0)
            {
                return null;
            }

            for (var i = 0; i < constraints.Count; i++)
            {
                if (constraints[i] is TDeliveryConstraint c)
                {
                    return c;
                }
            }

            return null;
        }

        static TimeSpan? ToNullIfNegative(TimeSpan value)
        {
            return value <= TimeSpan.Zero ? (TimeSpan?)null : value;
        }

        Task Send(MessageWrapper wrapper, QueueClient sendQueue, TimeSpan timeToBeReceived)
        {
            string base64String = MessageWrapperHelper.ConvertToBase64String(wrapper, serializer);

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
                Recoverable = operation.GetDeliveryConstraint<NonDurableDelivery>() == null,
                ReplyToAddress = headers.GetValueOrDefault(Headers.ReplyToAddress),
                Headers = headers,
                MessageIntent = operation.GetMessageIntent()
            };
        }

        readonly MessageWrapperSerializer serializer;

        readonly Func<UnicastTransportOperation, DateTimeOffset, CancellationToken, Task> scheduleMessageDelivery;

        readonly QueueAddressGenerator addressGenerator;
        readonly AzureStorageAddressingSettings addressing;
        readonly ConcurrentDictionary<string, Task<bool>> rememberExistence = new ConcurrentDictionary<string, Task<bool>>();
        readonly ISubscriptionStore subscriptionStore;

        static readonly TimeSpan CloudQueueMessageMaxTimeToLive = TimeSpan.FromDays(30);
        static readonly ILog Logger = LogManager.GetLogger<Dispatcher>();

        class MessageIdAndDestinationEqualityComparer : IEqualityComparer<UnicastTransportOperation>
        {
            public static MessageIdAndDestinationEqualityComparer Instance = new MessageIdAndDestinationEqualityComparer();

            public bool Equals(UnicastTransportOperation x, UnicastTransportOperation y)
            {
                if (ReferenceEquals(x, y))
                {
                    return true;
                }

                if (x is null)
                {
                    return false;
                }

                if (y is null)
                {
                    return false;
                }

                if (x.GetType() != y.GetType())
                {
                    return false;
                }

                return x.Destination == y.Destination && Equals(x.Message.MessageId, y.Message.MessageId);
            }

            public int GetHashCode(UnicastTransportOperation obj)
            {
                unchecked
                {
                    int hashCode = obj.Destination != null ? obj.Destination.GetHashCode() : 0;
                    hashCode = (hashCode * 397) ^ (obj.Message.MessageId != null ? obj.Message.MessageId.GetHashCode() : 0);
                    return hashCode;
                }
            }
        }
    }
}