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

    class AtLeastOnceReceiveStrategy : ReceiveStrategy
    {
        public AtLeastOnceReceiveStrategy(OnMessage onMessage, OnError onError, Action<string, Exception, CancellationToken> criticalErrorAction)
        {
            this.onMessage = onMessage;
            this.onError = onError;
            this.criticalErrorAction = criticalErrorAction;
        }

        public override async Task Receive(MessageRetrieved retrieved, MessageWrapper message, CancellationToken cancellationToken = default)
        {
            Logger.DebugFormat("Pushing received message (ID: '{0}') through pipeline.", message.Id);
            var body = message.Body ?? new byte[0];
            var contextBag = new ContextBag();
            try
            {
                var pushContext = new MessageContext(message.Id, new Dictionary<string, string>(message.Headers), body, new TransportTransaction(), contextBag);
                await onMessage(pushContext, cancellationToken).ConfigureAwait(false);

                await retrieved.Ack(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException oce)
            {
                // Graceful shutdown
                if (cancellationToken.IsCancellationRequested)
                {
                    Logger.Debug("Message processing cancelled. Rolling back transaction.", oce);
                }
                else
                {
                    Logger.Warn("OperationCanceledException thrown. Rolling back transaction.", oce);
                }

            }
            catch (LeaseTimeoutException)
            {
                // The lease has expired and cannot be used any longer to Ack or Nack the message.
                // see original issue: https://github.com/Azure/azure-storage-net/issues/285
                throw;
            }
            catch (Exception ex)
            {
                var context = CreateErrorContext(retrieved, message, ex, body, contextBag);

                try
                {
                    var errorHandleResult = await onError(context, cancellationToken).ConfigureAwait(false);

                    if (errorHandleResult == ErrorHandleResult.RetryRequired)
                    {
                        // For an immediate retry, the error is logged and the message is returned to the queue to preserve the DequeueCount.
                        // There is no in memory retry as scale-out scenarios would be handled improperly.
                        Logger.Warn("Azure Storage Queue transport failed pushing a message through pipeline. The message will be requeued", ex);
                        await retrieved.Nack(cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        // Just acknowledge the message as it's handled by the core retry.
                        await retrieved.Ack(cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException oce)
                {
                    // Graceful shutdown
                    if (cancellationToken.IsCancellationRequested)
                    {
                        Logger.Debug("Message processing cancelled. Rolling back transaction.", oce);
                    }
                    else
                    {
                        Logger.Warn("OperationCanceledException thrown. Rolling back transaction.", oce);
                    }

                }
                catch (Exception e)
                {
                    criticalErrorAction($"Failed to execute recoverability policy for message with native ID: `{message.Id}`", e, cancellationToken);

                    try
                    {
                        await retrieved.Nack(cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception e2)
                    {
                        Logger.Warn($"Failed to release visibility timeout after message with native ID `{message.Id}` failed to execute recoverability policy. The message will be available again when the visibility timeout expires.", e2);
                    }
                }
            }
        }

        readonly OnMessage onMessage;
        readonly OnError onError;
        readonly Action<string, Exception, CancellationToken> criticalErrorAction;

        static readonly ILog Logger = LogManager.GetLogger<ReceiveStrategy>();
    }
}