namespace NServiceBus.AzureStorageQueues
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Transports.WindowsAzureStorageQueues;
    using Extensibility;
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

        public override async Task Receive(MessageRetrieved retrieved, MessageWrapper message)
        {
            await retrieved.Ack().ConfigureAwait(false);
            var body = message.Body ?? new byte[0];

            try
            {
                var pushContext = new MessageContext(message.Id, message.Headers, body, new TransportTransaction(), new CancellationTokenSource(), new ContextBag());
                await pipeline(pushContext).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.Warn("Azure Storage Queue transport failed pushing a message through pipeline", ex);

                var context = CreateErrorContext(retrieved, message, ex, body);

                // The exception is pushed through the error pipeline in a fire and forget manner.
                // There's no call to onCriticalError if errorPipe fails. Exceptions are handled on the transport level.
                await errorPipe(context).ConfigureAwait(false);
            }
        }

        readonly Func<MessageContext, Task> pipeline;
        readonly Func<ErrorContext, Task<ErrorHandleResult>> errorPipe;

        static readonly ILog Logger = LogManager.GetLogger(typeof(ReceiveStrategy));
    }
}