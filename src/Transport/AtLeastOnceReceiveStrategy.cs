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
        public AtLeastOnceReceiveStrategy(OnMessage onMessage, OnError onError, Action<string, Exception, CancellationToken> criticalErrorAction)
        {
            this.onMessage = onMessage;
            this.onError = onError;
            this.criticalErrorAction = criticalErrorAction;
        }

        public override async Task Receive(MessageRetrieved retrieved, MessageWrapper message, string receiveAddress, CancellationToken cancellationToken = default)
        {
            if (messagesToBeAcked.TryGet(message.Id, out _))
            {
                try
                {
                    Logger.DebugFormat("Received message (ID: '{0}') was marked as successfully completed. Trying to immediately acknowledge the message without invoking the pipeline.", message.Id);
                    await retrieved.Ack(cancellationToken).ConfigureAwait(false);
                }
                // Doing a more generous catch here to make sure we are not losing the ID and can mark it to be completed another time
                catch (Exception ex) when (!ex.IsCausedBy(cancellationToken))
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
                var pushContext = new MessageContext(message.Id, new Dictionary<string, string>(message.Headers), body, new TransportTransaction(), receiveAddress, contextBag);
                await onMessage(pushContext, cancellationToken).ConfigureAwait(false);

                await retrieved.Ack(cancellationToken).ConfigureAwait(false);
            }
            catch (LeaseTimeoutException)
            {
                TrackMessageToBeCompletedOnNextReceive();
                throw;
            }
            catch (Exception ex) when (!ex.IsCausedBy(cancellationToken))
            {
                var context = CreateErrorContext(retrieved, message, ex, body, receiveAddress, contextBag);

                try
                {
                    var errorHandleResult = await onError(context, cancellationToken).ConfigureAwait(false);

                    if (errorHandleResult == ErrorHandleResult.RetryRequired)
                    {
                        // For an immediate retry, the error is logged and the message is returned to the queue to preserve the DequeueCount.
                        // There is no in memory retry as scale-out scenarios would be handled improperly.
                        Logger.Debug("Azure Storage Queue transport failed pushing a message through pipeline. The message will be requeued", ex);
                        await retrieved.Nack(cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        try
                        {
                            // Just acknowledge the message as it's handled by the core retry.
                            await retrieved.Ack(cancellationToken).ConfigureAwait(false);
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
                catch (Exception e) when (!e.IsCausedBy(cancellationToken))
                {
                    criticalErrorAction($"Failed to execute recoverability policy for message with native ID: `{message.Id}`", e, cancellationToken);

                    try
                    {
                        await retrieved.Nack(cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception nackEx) when (!nackEx.IsCausedBy(cancellationToken))
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

        readonly OnMessage onMessage;
        readonly OnError onError;
        readonly Action<string, Exception, CancellationToken> criticalErrorAction;
        readonly FastConcurrentLru<string, bool> messagesToBeAcked = new(1_000);

        static readonly ILog Logger = LogManager.GetLogger<ReceiveStrategy>();
    }
}