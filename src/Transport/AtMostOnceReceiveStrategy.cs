namespace NServiceBus.Transport.AzureStorageQueues
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Transports.WindowsAzureStorageQueues;
    using Extensibility;
    using Logging;
    using Transport;

    /// <summary>
    /// At-most-once receive strategy receives at most once, acking first, then processing the message.
    /// If the pipeline fails, the message is not processed any longer. No first or second level retries are executed.
    /// </summary>
    class AtMostOnceReceiveStrategy : ReceiveStrategy
    {
        public AtMostOnceReceiveStrategy(OnMessage onMessage, OnError onError)
        {
            this.onMessage = onMessage;
            this.onError = onError;
        }

        public override async Task Receive(MessageRetrieved retrieved, MessageWrapper message, CancellationToken cancellationToken)
        {
            Logger.DebugFormat("Pushing received message (ID: '{0}') through pipeline.", message.Id);
            await retrieved.Ack(cancellationToken).ConfigureAwait(false);
            var body = message.Body ?? new byte[0];
            var contextBag = new ContextBag();
            try
            {
                var pushContext = new MessageContext(message.Id, new Dictionary<string, string>(message.Headers), body, new TransportTransaction(), contextBag);
                await onMessage(pushContext, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Graceful shutdown
            }
            catch (Exception ex)
            {
                Logger.Warn("Azure Storage Queue transport failed pushing a message through pipeline", ex);

                var context = CreateErrorContext(retrieved, message, ex, body, contextBag);

                // The exception is pushed through the error pipeline in a fire and forget manner. Don't care about result.
                // There's no call to onCriticalError if errorPipe fails. Exceptions are handled on the transport level.
                _ = await onError(context, cancellationToken).ConfigureAwait(false);
            }
        }

        readonly OnMessage onMessage;
        readonly OnError onError;

        static readonly ILog Logger = LogManager.GetLogger<ReceiveStrategy>();
    }
}