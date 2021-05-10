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
        public AtMostOnceReceiveStrategy(OnMessage onMessage, OnError onError, Action<string, Exception, CancellationToken> criticalError)
        {
            this.onMessage = onMessage;
            this.onError = onError;
            this.criticalError = criticalError;
        }

        public override async Task Receive(MessageRetrieved retrieved, MessageWrapper message, CancellationToken cancellationToken = default)
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
            catch (OperationCanceledException oce)
            {
                // Graceful shutdown
                if (cancellationToken.IsCancellationRequested)
                {
                    Logger.Debug("Message processing cancelled.", oce);
                }
                else
                {
                    Logger.Warn("OperationCanceledException thrown.", oce);
                }

            }
            catch (Exception ex)
            {
                Logger.Warn("Azure Storage Queue transport failed pushing a message through pipeline", ex);

                try
                {
                    var context = CreateErrorContext(retrieved, message, ex, body, contextBag);
                    // Since this is TransportTransactionMode.None, we really don't care what the result is,
                    // we only need to know whether to call criticalErrorAction or not
                    _ = await onError(context, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException oce)
                {
                    // Graceful shutdown
                    if (cancellationToken.IsCancellationRequested)
                    {
                        Logger.Debug("Message processing cancelled.", oce);
                    }
                    else
                    {
                        Logger.Warn("OperationCanceledException thrown.", oce);
                    }

                }
                catch (Exception e)
                {
                    criticalError($"Failed to execute recoverability policy for message with native ID: `{message.Id}`", e, cancellationToken);
                }
            }
        }

        readonly OnMessage onMessage;
        readonly OnError onError;
        Action<string, Exception, CancellationToken> criticalError;

        static readonly ILog Logger = LogManager.GetLogger<ReceiveStrategy>();
    }
}