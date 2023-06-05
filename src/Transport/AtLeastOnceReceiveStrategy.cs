namespace NServiceBus.Transport.AzureStorageQueues
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Transports.WindowsAzureStorageQueues;
    using Extensibility;
    using global::Azure;
    using Logging;
    using Transport;

    /// <summary>
    /// This corresponds to the RecieveOnly transport transaction mode
    /// </summary>
    class AtLeastOnceReceiveStrategy : ReceiveStrategy
    {
        public AtLeastOnceReceiveStrategy(Func<MessageContext, Task> pipeline, Func<ErrorContext, Task<ErrorHandleResult>> errorPipe, CriticalError criticalError)
        {
            this.pipeline = pipeline;
            this.errorPipe = errorPipe;
            this.criticalError = criticalError;
        }

        public override async Task Receive(MessageRetrieved retrieved, MessageWrapper message, CancellationToken cancellationToken = default)
        {
            Logger.DebugFormat("Pushing received message (ID: '{0}') through pipeline.", message.Id);
            var body = message.Body ?? Array.Empty<byte>();
            try
            {
                using (var tokenSource = new CancellationTokenSource())
                {
                    var pushContext = new MessageContext(message.Id, new Dictionary<string, string>(message.Headers), body, new TransportTransaction(), tokenSource, new ContextBag());
                    await pipeline(pushContext).ConfigureAwait(false);

                    if (tokenSource.IsCancellationRequested)
                    {
                        // if the pipeline cancelled the execution, nack the message to go back to the queue
                        await retrieved.Nack().ConfigureAwait(false);
                    }
                    else
                    {
                        // the pipeline hasn't been cancelled, the message should be acked
                        await retrieved.Ack().ConfigureAwait(false);
                    }
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
                var context = CreateErrorContext(retrieved, message, ex, body);
                ErrorHandleResult immediateRetry;

                try
                {
                    immediateRetry = await errorPipe(context).ConfigureAwait(false);
                }
                catch (RequestFailedException e) when (e.Status == 413 && e.ErrorCode == "RequestBodyTooLarge")
                {
                    Logger.WarnFormat($"Message with native ID `{message.Id}` could not be moved to the error queue with additional headers because it was too large. Moving to the error queue as is.", e);

                    await retrieved.MoveToErrorQueueWithMinimalFaultHeaders(context, cancellationToken).ConfigureAwait(false);

                    return;
                }
                catch (Exception e)
                {
                    criticalError.Raise($"Failed to execute recoverability policy for message with native ID: `{message.Id}`", e);

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

        readonly Func<MessageContext, Task> pipeline;
        readonly Func<ErrorContext, Task<ErrorHandleResult>> errorPipe;
        readonly CriticalError criticalError;

        static readonly ILog Logger = LogManager.GetLogger<ReceiveStrategy>();
    }
}