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

        public override async Task Receive(MessageRetrieved retrieved, MessageWrapper message)
        {
            Logger.DebugFormat("Pushing received message (ID: '{0}') through pipeline.", message.Id);
            var body = message.Body ?? new byte[0];
            var contextBag = new ContextBag();
            try
            {
                //TODO: what this should look like given the new cancellation support?
                // https://github.com/Particular/NServiceBus.AzureStorageQueues/issues/526

                var pushContext = new MessageContext(message.Id, new Dictionary<string, string>(message.Headers), body, new TransportTransaction(), contextBag);
                await onMessage(pushContext, CancellationToken.None).ConfigureAwait(false);

                //if (tokenSource.IsCancellationRequested)
                //{
                //    // if the pipeline canceled the execution, nack the message to go back to the queue
                //    await retrieved.Nack().ConfigureAwait(false);
                //}
                //else
                //{
                //  // the pipeline hasn't been canceled, the message should be acked
                //  await retrieved.Ack().ConfigureAwait(false);
                //}

                await retrieved.Ack().ConfigureAwait(false);
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
                ErrorHandleResult immediateRetry;

                try
                {
                    immediateRetry = await onError(context, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    criticalErrorAction($"Failed to execute recoverability policy for message with native ID: `{message.Id}`", e, CancellationToken.None);

                    await retrieved.Nack().ConfigureAwait(false);

                    return;
                }

                if (immediateRetry == ErrorHandleResult.RetryRequired)
                {
                    // For an immediate retry, the error is logged and the message is returned to the queue to preserve the DequeueCount.
                    // There is no in memory retry as scale-out scenarios would be handled improperly.
                    Logger.Warn("Azure Storage Queue transport failed pushing a message through pipeline. The message will be requeued", ex);
                    await retrieved.Nack().ConfigureAwait(false);
                }
                else
                {
                    // Just acknowledge the message as it's handled by the core retry.
                    await retrieved.Ack().ConfigureAwait(false);
                }
            }
        }

        readonly OnMessage onMessage;
        readonly OnError onError;
        readonly Action<string, Exception, CancellationToken> criticalErrorAction;

        static readonly ILog Logger = LogManager.GetLogger<ReceiveStrategy>();
    }
}