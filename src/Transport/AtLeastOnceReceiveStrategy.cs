namespace NServiceBus.Transport.AzureStorageQueues
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Azure.Transports.WindowsAzureStorageQueues;
    using Extensibility;
    using Logging;
    using Transport;

    class AtLeastOnceReceiveStrategy : ReceiveStrategy
    {
        public AtLeastOnceReceiveStrategy(OnMessage onMessage, OnError onError, Action<string, Exception> criticalErrorAction)
        {
            this.onMessage = onMessage;
            this.onError = onError;
            this.criticalErrorAction = criticalErrorAction;
        }

        public override async Task Receive(MessageRetrieved retrieved, MessageWrapper message)
        {
            Logger.DebugFormat("Pushing received message (ID: '{0}') through pipeline.", message.Id);
            var body = message.Body ?? new byte[0];
            try
            {
                var pushContext = new MessageContext(message.Id, new Dictionary<string, string>(message.Headers), body, new TransportTransaction(), new ContextBag());
                await onMessage(pushContext).ConfigureAwait(false);

                //TODO: what this should look like given the new cancellation support?
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
                var context = CreateErrorContext(retrieved, message, ex, body);
                ErrorHandleResult immediateRetry;

                try
                {
                    immediateRetry = await onError(context).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    criticalErrorAction($"Failed to execute recoverability policy for message with native ID: `{message.Id}`", e);

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
        readonly Action<string, Exception> criticalErrorAction;

        static readonly ILog Logger = LogManager.GetLogger<ReceiveStrategy>();
    }
}