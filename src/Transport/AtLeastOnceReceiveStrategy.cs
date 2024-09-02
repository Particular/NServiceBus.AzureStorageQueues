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
            Logger.DebugFormat("Pushing received message (ID: '{0}') through pipeline.", message.Id);

            var nativeMessageId = retrieved.NativeMessageId;
            if (messagesToBeDeleted.TryGet(nativeMessageId, out _))
            {
                await DeleteMessage(retrieved, nativeMessageId, cancellationToken).ConfigureAwait(false);
                return;
            }
            var messageProcessed = await InnerProcessMessage(retrieved, message, receiveAddress, nativeMessageId, cancellationToken).ConfigureAwait(false);

            if (messageProcessed)
            {
                await DeleteMessage(retrieved, nativeMessageId, cancellationToken).ConfigureAwait(false);
            }
        }

        async Task<bool> InnerProcessMessage(MessageRetrieved retrieved, MessageWrapper message, string receiveAddress, string nativeMessageId, CancellationToken cancellationToken = default)
        {
            var body = message.Body ?? Array.Empty<byte>();
            var contextBag = new ContextBag();

            try
            {
                var pushContext = new MessageContext(retrieved.NativeMessageId, new Dictionary<string, string>(message.Headers), body, new TransportTransaction(), receiveAddress, contextBag);

                await onMessage(pushContext, cancellationToken).ConfigureAwait(false);

            }
            catch (Exception ex) when (!ex.IsCausedBy(cancellationToken))
            {
                var deliveryAttempts = GetDeliveryAttempts(nativeMessageId);
                var context = CreateErrorContext(retrieved, message, ex, body, receiveAddress, contextBag, deliveryAttempts);
                try
                {
                    var errorHandleResult = await onError(context, cancellationToken).ConfigureAwait(false);

                    if (errorHandleResult == ErrorHandleResult.RetryRequired)
                    {
                        // For an immediate retry, the error is logged and the message is returned to the queue to preserve the DequeueCount.
                        // There is no in memory retry as scale-out scenarios would be handled improperly.
                        Logger.Warn("Azure Storage Queue transport failed pushing a message through pipeline. The message will be requeued", ex);
                        await retrieved.ReturnMessageToQueue(cancellationToken).ConfigureAwait(false);
                        return false;
                    }
                }
                catch (RequestFailedException e) when (e.Status == 413 && e.ErrorCode == "RequestBodyTooLarge")
                {
                    Logger.WarnFormat($"Message with native ID `{nativeMessageId}` could not be moved to the error queue with additional headers because it was too large. Moving to the error queue as is.", e);

                    await retrieved.MoveToErrorQueueWithMinimalFaultHeaders(context, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception onErrorEx) when (!onErrorEx.IsCausedBy(cancellationToken))
                {
                    criticalErrorAction($"Failed to execute recoverability policy for message with native ID: `{nativeMessageId}`", onErrorEx, cancellationToken);
                    await retrieved.ReturnMessageToQueue(cancellationToken).ConfigureAwait(false);
                    return false;
                }
            }

            return true;
        }

        async Task DeleteMessage(MessageRetrieved retrieved, string nativeMessageId, CancellationToken cancellationToken = default)
        {
            try
            {
                await retrieved.DeleteMessage(cancellationToken).ConfigureAwait(false);
            }
            catch (LeaseTimeoutException ex)
            {
                Logger.Error($"Failed to delete message with native ID '{nativeMessageId}' because the handler execution time exceeded the visibility timeout. Increase the length of the timeout on the queue. The message was returned to the queue.", ex);

                messagesToBeDeleted.AddOrUpdate(nativeMessageId, true);
            }
            catch (Exception ex) when (!ex.IsCausedBy(cancellationToken))
            {
                Logger.Warn($"Failed to delete message with native ID '{nativeMessageId}'. The message will be returned to the queue when the visibility timeout expires.", ex);

                messagesToBeDeleted.AddOrUpdate(nativeMessageId, true);
            }
        }


        int GetDeliveryAttempts(string nativeMessageId)
        {
            var attempts = deliveryAttempts.GetOrAdd(nativeMessageId, k => 0);
            attempts++;
            deliveryAttempts.AddOrUpdate(nativeMessageId, attempts);

            return attempts;
        }
        readonly FastConcurrentLru<string, int> deliveryAttempts = new(1_000);
        readonly FastConcurrentLru<string, bool> messagesToBeDeleted = new(1_000);
        readonly OnMessage onMessage;
        readonly OnError onError;
        readonly Action<string, Exception, CancellationToken> criticalErrorAction;

        static readonly ILog Logger = LogManager.GetLogger<ReceiveStrategy>();
    }
}