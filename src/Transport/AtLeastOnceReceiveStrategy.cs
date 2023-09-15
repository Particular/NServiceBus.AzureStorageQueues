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
            if (messagesFailedToAck.TryGet(message.Id, out _))
            {
                try
                {
                    await retrieved.Ack(cancellationToken).ConfigureAwait(false);
                    messagesFailedToAck.TryRemove(message.Id);
                }
                catch (LeaseTimeoutException ex)
                {
                    Logger.Warn($"Failed acknowledge the message with native ID `{message.Id}`. The message will be available again when the visibility timeout expires.", ex);
                }

                return;
            }

            if (Logger.IsDebugEnabled)
            {
                Logger.DebugFormat("Pushing received message (ID: '{0}') through pipeline.", message.Id);
            }

            var body = message.Body ?? Array.Empty<byte>();
            var contextBag = new ContextBag();
            try
            {
                var pushContext = new MessageContext(message.Id, new Dictionary<string, string>(message.Headers), body, new TransportTransaction(), receiveAddress, contextBag);
                await onMessage(pushContext, cancellationToken).ConfigureAwait(false);

                await retrieved.Ack(cancellationToken).ConfigureAwait(false);
            }
            catch (LeaseTimeoutException ex)
            {
                Logger.Warn($"Failed acknowledge the message with native ID `{message.Id}`. The message was returned to the queue and will be available again when the visibility timeout expires.", ex);
                messagesFailedToAck.AddOrUpdate(message.Id, true);
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
                        try
                        {
                            await retrieved.Nack(cancellationToken).ConfigureAwait(false);
                        }
                        catch (LeaseTimeoutException leaseNackException)
                        {
                            Logger.Warn($"Failed to release visibility timeout after message with native ID `{message.Id}`. The message will be available again when the visibility timeout expires.", leaseNackException);
                            throw;
                        }
                    }
                    else
                    {
                        try
                        {
                            await retrieved.Ack(cancellationToken).ConfigureAwait(false);
                        }
                        catch (LeaseTimeoutException leaseAckException)
                        {
                            Logger.Warn($"Failed acknowledge the message with native ID `{message.Id}`. The message was returned to the queue and will be available again when the visibility timeout expires.", leaseAckException);
                            messagesFailedToAck.AddOrUpdate(message.Id, true);
                        }
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
                    }
                }
            }
        }

        readonly OnMessage onMessage;
        readonly OnError onError;
        readonly Action<string, Exception, CancellationToken> criticalErrorAction;
        // TODO align with concurrency?
        readonly FastConcurrentLru<string, bool> messagesFailedToAck = new(1000);

        static readonly ILog Logger = LogManager.GetLogger<ReceiveStrategy>();
    }
}