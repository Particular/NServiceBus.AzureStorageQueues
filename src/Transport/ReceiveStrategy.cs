namespace NServiceBus.Transport.AzureStorageQueues
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Transports.WindowsAzureStorageQueues;
    using Extensibility;
    using Transport;

    abstract class ReceiveStrategy
    {
        public abstract Task Receive(MessageRetrieved retrieved, MessageWrapper message);

        public static ReceiveStrategy BuildReceiveStrategy(OnMessage onMessage, OnError onError, TransportTransactionMode transactionMode, Action<string, Exception, CancellationToken> criticalErrorAction)
        {
            switch (transactionMode)
            {
                case TransportTransactionMode.None:
                    return new AtMostOnceReceiveStrategy(onMessage, onError);
                case TransportTransactionMode.ReceiveOnly:
                    return new AtLeastOnceReceiveStrategy(onMessage, onError, criticalErrorAction);
                case TransportTransactionMode.SendsAtomicWithReceive:
                case TransportTransactionMode.TransactionScope:
                default:
                    throw new NotSupportedException($"The TransportTransactionMode {transactionMode} is not supported");
            }
        }

        protected static ErrorContext CreateErrorContext(MessageRetrieved retrieved, MessageWrapper message, Exception ex, byte[] body, ReadOnlyContextBag contextBag)
        {
            var context = new ErrorContext(ex, message.Headers, message.Id, body, new TransportTransaction(), Convert.ToInt32(retrieved.DequeueCount), contextBag);
            return context;
        }
    }
}