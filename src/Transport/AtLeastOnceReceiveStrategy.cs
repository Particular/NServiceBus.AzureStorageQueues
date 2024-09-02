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
            var body = message.Body ?? Array.Empty<byte>();
            var contextBag = new ContextBag();
            if (messagesToBeDeleted.TryGet(retrieved.NativeMessageId, out _))
            {
                await retrieved.Ack(cancellationToken).ConfigureAwait(false);
                return;
            }
            try
            {
                var pushContext = new MessageContext(message.Id, new Dictionary<string, string>(message.Headers), body, new TransportTransaction(), receiveAddress, contextBag);
                await onMessage(pushContext, cancellationToken).ConfigureAwait(false);

                await retrieved.Ack(cancellationToken).ConfigureAwait(false);
            }
            catch (LeaseTimeoutException ex)
            {
                // The lease has expired and cannot be used any longer to Ack or Nack the message.
                // see original issue: https://github.com/Azure/azure-storage-net/issues/285
                //throw;
                Logger.Warn("Dispatching the message took longer than a visibility timeout. The message will reappear in the queue and will be obtained again.", ex);
                messagesToBeDeleted.AddOrUpdate(retrieved.NativeMessageId, true);
            }
            catch (Exception ex) when (!ex.IsCausedBy(cancellationToken))
            {
                var deliveryAttempts = GetDeliveryAttempts(retrieved.NativeMessageId);
                //var context = CreateErrorContext(retrieved, message, ex, body, receiveAddress, contextBag);
                var context = CreateErrorContext(retrieved, message, ex, body, receiveAddress, contextBag, deliveryAttempts);
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
                catch (RequestFailedException e) when (e.Status == 413 && e.ErrorCode == "RequestBodyTooLarge")
                {
                    Logger.WarnFormat($"Message with native ID `{message.Id}` could not be moved to the error queue with additional headers because it was too large. Moving to the error queue as is.", e);

                    await retrieved.MoveToErrorQueueWithMinimalFaultHeaders(context, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception onErrorEx) when (!onErrorEx.IsCausedBy(cancellationToken))
                {
                    criticalErrorAction($"Failed to execute recoverability policy for message with native ID: `{message.Id}`", onErrorEx, cancellationToken);

                    try
                    {
                        await retrieved.Nack(cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception nackEx) when (!nackEx.IsCausedBy(cancellationToken))
                    {
                        Logger.Warn($"Failed to release visibility timeout after message with native ID `{message.Id}` failed to execute recoverability policy. The message will be available again when the visibility timeout expires.", nackEx);
                        messagesToBeDeleted.AddOrUpdate(retrieved.NativeMessageId, true);
                    }
                }
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