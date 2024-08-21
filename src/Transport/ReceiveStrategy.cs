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
        public abstract Task Receive(MessageRetrieved retrieved, MessageWrapper message, string receiveAddress, CancellationToken cancellationToken = default);

        public static ReceiveStrategy BuildReceiveStrategy(OnMessage onMessage, OnError onError, TransportTransactionMode transactionMode, Action<string, Exception, CancellationToken> criticalErrorAction) => transactionMode switch
        {
            TransportTransactionMode.None => new AtMostOnceReceiveStrategy(onMessage, onError, criticalErrorAction),
            TransportTransactionMode.ReceiveOnly => new AtLeastOnceReceiveStrategy(onMessage, onError, criticalErrorAction),
            TransportTransactionMode.SendsAtomicWithReceive => throw new NotSupportedException($"The TransportTransactionMode {transactionMode} is not supported"),
            TransportTransactionMode.TransactionScope => throw new NotSupportedException($"The TransportTransactionMode {transactionMode} is not supported"),
            _ => throw new NotSupportedException($"The TransportTransactionMode {transactionMode} is not supported")
        };

        protected static ErrorContext CreateErrorContext(MessageRetrieved retrieved, MessageWrapper message, Exception ex, byte[] body, string receiveAddress, ContextBag contextBag)
        {
            var context = new ErrorContext(ex, message.Headers, message.Id, body, new TransportTransaction(), Convert.ToInt32(retrieved.DequeueCount), receiveAddress, contextBag);
            return context;
        }
        protected static ErrorContext CreateErrorContext(MessageRetrieved retrieved, MessageWrapper message, Exception ex, byte[] body, string receiveAddress, ContextBag contextBag, int deliveryAttempts)
        {
            var context = new ErrorContext(ex, message.Headers, retrieved.NativeMessageId, body, new TransportTransaction(), deliveryAttempts, receiveAddress, contextBag);
            return context;
        }
    }
}