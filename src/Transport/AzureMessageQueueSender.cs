namespace NServiceBus.Azure.Transports.WindowsAzureStorageQueues
{
    using System;
    using System.Collections.Concurrent;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Transactions;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Queue;
    using NServiceBus.DeliveryConstraints;
    using NServiceBus.Extensibility;
    using NServiceBus.Logging;
    using NServiceBus.Performance.TimeToBeReceived;
    using NServiceBus.Serialization;
    using NServiceBus.Settings;
    using NServiceBus.Transports;
    using NServiceBus.Unicast.Queuing;

    public class AzureMessageQueueSender : IDispatchMessages
    {
        static readonly ConcurrentDictionary<string, bool> rememberExistence = new ConcurrentDictionary<string, bool>();
        readonly ICreateQueueClients createQueueClients;
        readonly ILog logger = LogManager.GetLogger(typeof(AzureMessageQueueSender));
        readonly IMessageSerializer messageSerializer;
        private readonly bool transactionsEnabled;
        readonly DeterminesBestConnectionStringForStorageQueues validation;

        public AzureMessageQueueSender(ICreateQueueClients createQueueClients, IMessageSerializer messageSerializer, ReadOnlySettings settings
            , string defaultConnectionString)
        {
            this.createQueueClients = createQueueClients;
            this.messageSerializer = messageSerializer;
            validation = new DeterminesBestConnectionStringForStorageQueues(settings, defaultConnectionString);
            transactionsEnabled = settings.Get<bool>("Transactions.Enabled");
        }

        public Task Dispatch(TransportOperations outgoingMessages, ContextBag context)
        {
            if (outgoingMessages.MulticastTransportOperations.Any())
            {
                throw new Exception("The Azure Storage Queue transport only supports unicast transport operations.");
            }

            foreach (var unicastTransportOperation in outgoingMessages.UnicastTransportOperations)
            {
                Send(unicastTransportOperation);
            }

            return Task.FromResult(0);
        }

        private void Send(UnicastTransportOperation operation)
        {
            var address = operation.Destination;

            // TODO: is it address or connection string?
            var sendClient = createQueueClients.Create(address);
            var sendQueue = sendClient.GetQueueReference(AzureMessageQueueUtils.GetQueueName(address));

            if (!Exists(sendQueue))
            {
                throw new QueueNotFoundException
                {
                    Queue = address
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
                sendQueue.AddMessage(rawMessage, timeToBeReceived);
            }
            else
            {
                Transaction.Current.EnlistVolatile(new SendResourceManager(sendQueue, rawMessage, timeToBeReceived), EnlistmentOptions.None);
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

    internal static class TransportOperationExtensions
    {
        public static TimeSpan? GetTimeToBeReceived(this UnicastTransportOperation operation)
        {
            return operation.GetDeliveryConstraint<DiscardIfNotReceivedBefore>()?.MaxTime;
        }

        public static T GetDeliveryConstraint<T>(this IOutgoingTransportOperation operation)
            where T : DeliveryConstraint
        {
            return operation.DeliveryConstraints.OfType<T>().FirstOrDefault();
        }
    }

    public class CreateQueueClients : ICreateQueueClients
    {
        readonly ConcurrentDictionary<string, CloudQueueClient> destinationQueueClients = new ConcurrentDictionary<string, CloudQueueClient>();
        readonly DeterminesBestConnectionStringForStorageQueues validation;

        public CreateQueueClients(ReadOnlySettings settings, string defaultConnectionString)
        {
            validation = new DeterminesBestConnectionStringForStorageQueues(settings, defaultConnectionString);
        }

        public CloudQueueClient Create(string connectionString)
        {
            return destinationQueueClients.GetOrAdd(connectionString, s =>
            {
                if (!validation.IsPotentialStorageQueueConnectionString(connectionString))
                {
                    connectionString = validation.Determine();
                }

                CloudQueueClient sendClient = null;
                CloudStorageAccount account;

                if (CloudStorageAccount.TryParse(connectionString, out account))
                {
                    sendClient = account.CreateCloudQueueClient();
                }

                return sendClient;
            });
        }
    }

    public interface ICreateQueueClients
    {
        CloudQueueClient Create(string connectionString);
    }
}