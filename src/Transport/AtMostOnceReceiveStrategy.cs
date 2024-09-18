namespace NServiceBus.Transport.AzureStorageQueues
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Transports.WindowsAzureStorageQueues;
    using Extensibility;
    using global::Azure;
    using global::Azure.Storage.Queues.Models;
    using Logging;
    using Transport;

    /// <summary>
    /// At-most-once receive strategy receives at most once, acking first, then processing the message.
    /// If the pipeline fails, the message is not processed any longer. No first or second level retries are executed.
    /// </summary>
    class AtMostOnceReceiveStrategy : ReceiveStrategy
    {
        public AtMostOnceReceiveStrategy(Func<MessageContext, Task> pipeline, Func<ErrorContext, Task<ErrorHandleResult>> errorPipe)
        {
            this.pipeline = pipeline;
            this.errorPipe = errorPipe;
        }

        public override async Task Receive(MessageRetrieved retrieved, MessageWrapper message, CancellationToken cancellationToken = default)
        {
            Logger.DebugFormat("Pushing received message (ID: '{0}') through pipeline.", message.Id);
            await retrieved.Ack().ConfigureAwait(false);
            var body = message.Body ?? Array.Empty<byte>();
            var contextBag = new ContextBag();
            contextBag.Set<QueueMessage>(retrieved);

            try
            {
                var pushContext = new MessageContext(message.Id, new Dictionary<string, string>(message.Headers), body, new TransportTransaction(), new CancellationTokenSource(), contextBag);
                await pipeline(pushContext).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.Warn("Azure Storage Queue transport failed pushing a message through pipeline", ex);

                var context = CreateErrorContext(retrieved, message, ex, body, contextBag);
                try
                {
                    // The exception is pushed through the error pipeline in a fire and forget manner.
                    // There's no call to onCriticalError if errorPipe fails. Exceptions are handled on the transport level.
                    await errorPipe(context).ConfigureAwait(false);
                }
                catch (RequestFailedException e) when (e.Status == 413 && e.ErrorCode == "RequestBodyTooLarge")
                {
                    Logger.WarnFormat($"Message with native ID `{message.Id}` could not be moved to the error queue with additional headers because it was too large. Moving to the error queue as is.", e);

                    await retrieved.MoveToErrorQueueWithMinimalFaultHeaders(context, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        readonly Func<MessageContext, Task> pipeline;
        readonly Func<ErrorContext, Task<ErrorHandleResult>> errorPipe;

        static readonly ILog Logger = LogManager.GetLogger<ReceiveStrategy>();
    }
}