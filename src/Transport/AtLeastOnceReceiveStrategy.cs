namespace NServiceBus.Transport.AzureStorageQueues
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Transports.WindowsAzureStorageQueues;
    using BitFaster.Caching.Lru;
    using Extensibility;
    using global::Azure;
    using global::Azure.Storage.Queues.Models;
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
            if (messagesToBeAcked.TryGet(message.Id, out _))
            {
                try
                {
                    Logger.DebugFormat("Received message (ID: '{0}') was marked as successfully completed. Trying to immediately acknowledge the message without invoking the pipeline.", message.Id);
                    await retrieved.Ack().ConfigureAwait(false);
                }
                // Doing a more generous catch here to make sure we are not losing the ID and can mark it to be completed another time
                catch (Exception)
                {
                    TrackMessageToBeCompletedOnNextReceive();
                    throw;
                }
                return;
            }

            Logger.DebugFormat("Pushing received message (ID: '{0}') through pipeline.", message.Id);
            var body = message.Body ?? Array.Empty<byte>();
            var contextBag = new ContextBag();
            contextBag.Set<QueueMessage>(retrieved);

            try
            {
                using (var tokenSource = new CancellationTokenSource())
                {
                    var pushContext = new MessageContext(message.Id, new Dictionary<string, string>(message.Headers), body, new TransportTransaction(), tokenSource, contextBag);
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
                TrackMessageToBeCompletedOnNextReceive();
                throw;
            }
            catch (Exception ex)
            {
                var context = CreateErrorContext(retrieved, message, ex, body, contextBag);

                try
                {
                    var errorHandleResult = await errorPipe(context).ConfigureAwait(false);

                    if (errorHandleResult == ErrorHandleResult.RetryRequired)
                    {
                        // For an immediate retry, the error is logged and the message is returned to the queue to preserve the DequeueCount.
                        // There is no in memory retry as scale-out scenarios would be handled improperly.
                        Logger.Debug("Azure Storage Queue transport failed pushing a message through pipeline. The message will be requeued", ex);
                        await retrieved.Nack().ConfigureAwait(false);
                    }
                    else
                    {
                        try
                        {
                            // Just acknowledge the message as it's handled by the core retry.
                            await retrieved.Ack().ConfigureAwait(false);
                        }
                        catch (LeaseTimeoutException)
                        {
                            TrackMessageToBeCompletedOnNextReceive();
                            throw;
                        }
                    }
                }
                catch (RequestFailedException e) when (e.Status == 413 && e.ErrorCode == "RequestBodyTooLarge")
                {
                    Logger.Warn($"Message with native ID `{message.Id}` could not be moved to the error queue with additional headers because it was too large. Moving to the error queue as is.", e);

                    await retrieved.MoveToErrorQueueWithMinimalFaultHeaders(context, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    criticalError.Raise($"Failed to execute recoverability policy for message with native ID: `{message.Id}`", e);

                    try
                    {
                        await retrieved.Nack().ConfigureAwait(false);
                    }
                    catch (Exception nackEx)
                    {
                        Logger.Warn($"Failed to release visibility timeout after message with native ID `{message.Id}` failed to execute recoverability policy. The message will be available again when the visibility timeout expires.", nackEx);
                    }
                }
            }

            return;

            void TrackMessageToBeCompletedOnNextReceive() =>
                // The raw message ID might not be stable across retries, so we use the message wrapper ID instead.
                messagesToBeAcked.AddOrUpdate(message.Id, true);
        }

        readonly Func<MessageContext, Task> pipeline;
        readonly Func<ErrorContext, Task<ErrorHandleResult>> errorPipe;
        readonly CriticalError criticalError;
        readonly FastConcurrentLru<string, bool> messagesToBeAcked = new FastConcurrentLru<string, bool>(1_000);

        static readonly ILog Logger = LogManager.GetLogger<ReceiveStrategy>();
    }
}