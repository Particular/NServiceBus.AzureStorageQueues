namespace NServiceBus.AzureStorageQueues
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Transports.WindowsAzureStorageQueues;
    using Extensibility;
    using Logging;
    using Transport;

    abstract class ReceiveStrategy
    {
        public abstract Task Receive(MessageRetrieved retrieved, MessageWrapper message);

        public static ReceiveStrategy BuildReceiveStrategy(Func<MessageContext, Task> pipe, Func<ErrorContext, Task<ErrorHandleResult>> errorPipe, TransportTransactionMode transactionMode)
        {
            // ReSharper disable once SwitchStatementMissingSomeCases
            switch (transactionMode)
            {
                case TransportTransactionMode.None:
                    return new AtMostOnceReceiveStrategy(pipe, errorPipe);
                case TransportTransactionMode.ReceiveOnly:
                    return new AtLeastOnceReceiveStrategy(pipe, errorPipe);
                default:
                    throw new NotSupportedException($"The TransportTransactionMode {transactionMode} is not supported");
            }
        }

        static ErrorContext CreateErrorContext(MessageRetrieved retrieved, MessageWrapper message, Exception ex, byte[] body)
        {
            var context = new ErrorContext(ex, message.Headers, message.Id, body, new TransportTransaction(), retrieved.DequeueCount);
            return context;
        }

        static ILog Logger = LogManager.GetLogger(typeof(ReceiveStrategy));

        /// <summary>
        /// At-most-once receive strategy receives at most once, acking first, then processing the message.
        /// If the pipeline fails, the message is not processed any longer. No first or second level retries are executed.
        /// </summary>
        sealed class AtMostOnceReceiveStrategy : ReceiveStrategy
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
        }

        sealed class AtLeastOnceReceiveStrategy : ReceiveStrategy
        {
            public AtLeastOnceReceiveStrategy(Func<MessageContext, Task> pipeline, Func<ErrorContext, Task<ErrorHandleResult>> errorPipe)
            {
                this.pipeline = pipeline;
                this.errorPipe = errorPipe;
            }

            public override async Task Receive(MessageRetrieved retrieved, MessageWrapper message)
            {
                var body = message.Body ?? new byte[0];
                try
                {
                    using (var tokenSource = new CancellationTokenSource())
                    {
                        var pushContext = new MessageContext(message.Id, message.Headers, body, new TransportTransaction(), tokenSource, new ContextBag());
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
                    catch (Exception e)
                    {
                        Logger.Warn("The error pipeline wasn't able to handle the exception.", e);
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
        }
    }
}