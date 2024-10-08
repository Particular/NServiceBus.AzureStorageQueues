﻿namespace NServiceBus.Transport.AzureStorageQueues
{
    using System;
    using System.Runtime.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Transports.WindowsAzureStorageQueues;
    using global::Azure;
    using global::Azure.Storage.Queues;
    using global::Azure.Storage.Queues.Models;
    using Logging;
    using NServiceBus.Faults;

    class MessageRetrieved
    {
        public MessageRetrieved(IMessageEnvelopeUnwrapper unwrapper, MessageWrapperSerializer serializer, QueueMessage rawMessage, QueueClient inputQueue, QueueClient errorQueue, DateTimeOffset messageReceivedTime, TimeProvider timeProvider)
        {
            this.unwrapper = unwrapper;
            this.serializer = serializer;
            this.errorQueue = errorQueue;
            this.rawMessage = rawMessage;
            this.messageReceivedTime = messageReceivedTime;
            this.timeProvider = timeProvider;
            this.inputQueue = inputQueue;
            startTimestamp = this.timeProvider.GetTimestamp();
        }

        public long DequeueCount => rawMessage.DequeueCount;

        /// <summary>
        /// Unwraps the raw message body.
        /// </summary>
        /// <exception cref="SerializationException">Thrown when the raw message could not be unwrapped. The raw message is automatically moved to the error queue before this exception is thrown.</exception>
        /// <returns>The actual message wrapper.</returns>
        public async Task<MessageWrapper> Unwrap(CancellationToken cancellationToken = default)
        {
            try
            {
                Logger.DebugFormat("Unwrapping message with native ID: '{0}'", rawMessage.MessageId);
                unwrappedMessage ??= unwrapper.Unwrap(rawMessage);
                return unwrappedMessage;
            }
            catch (Exception ex)
            {
                await MoveToErrorQueueWithoutModification(cancellationToken).ConfigureAwait(false);

                throw new SerializationException($"Failed to deserialize message envelope for message with id {rawMessage.MessageId}. Make sure the configured serializer is used across all endpoints or configure the message wrapper serializer for this endpoint using the `SerializeMessageWrapperWith` extension on the transport configuration. Please refer to the Azure Storage Queue Transport configuration documentation for more details.", ex);
            }
        }

        /// <summary>
        /// Acknowledges the successful processing of the message.
        /// </summary>
        public async Task Ack(CancellationToken cancellationToken = default)
        {
            AssertVisibilityTimeout();

            try
            {
                await inputQueue.DeleteMessageAsync(rawMessage.MessageId, rawMessage.PopReceipt, cancellationToken).ConfigureAwait(false);
            }
            // AssertVisibilityTimeout might suffer from clock drifts, so we need to handle the case when the message is not found
            // which might indicate the message visibility timeout has expired.
            catch (RequestFailedException ex) when (ex.ErrorCode == QueueErrorCode.MessageNotFound)
            {
                throw new LeaseTimeoutException(rawMessage, visibilityTimeoutExceededBy: TimeSpan.Zero);
            }
        }

        /// <summary>
        /// Moves the message without expiry to the error queue
        /// </summary>
        public async Task MoveToErrorQueueWithoutModification(CancellationToken cancellationToken = default)
        {
            // When a CloudQueueMessage is retrieved and is en-queued directly, message's ID and PopReceipt are mutated.
            // To be able to delete the original message, original message ID and PopReceipt have to be stored aside.
            var messageId = rawMessage.MessageId;
            var messagePopReceipt = rawMessage.PopReceipt;

            await MoveToErrorQueue(messageId, messagePopReceipt, rawMessage.Body, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Moves the message without expiry to the error queue with minimal fault headers
        /// </summary>
        public async Task MoveToErrorQueueWithMinimalFaultHeaders(ErrorContext context, CancellationToken cancellationToken = default)
        {
            var unwrappedMessage = await Unwrap(cancellationToken).ConfigureAwait(false);

            unwrappedMessage.Headers.Add(FaultsHeaderKeys.FailedQ, inputQueue.Name);
            unwrappedMessage.Headers.Add("NServiceBus.ExceptionInfo.ExceptionType", context.Exception.GetType().FullName);

            var body = ReWrap(unwrappedMessage);

            await MoveToErrorQueue(rawMessage.MessageId, rawMessage.PopReceipt, body, cancellationToken).ConfigureAwait(false);
        }

        public async Task MoveToErrorQueue(string messageId, string messagePopReceipt, BinaryData body, CancellationToken cancellationToken = default)
        {
            try
            {
                await errorQueue.SendMessageAsync(body, timeToLive: TimeSpan.FromSeconds(-1), cancellationToken: cancellationToken).ConfigureAwait(false);
                // TODO: might not need this as the new SDK doesn't send a message by using the original message. Rather, copies the text only.
                await inputQueue.DeleteMessageAsync(messageId, messagePopReceipt, cancellationToken).ConfigureAwait(false);
            }
            catch (RequestFailedException e) when (e.Status == 413 && e.ErrorCode == "RequestBodyTooLarge")
            {
                Logger.WarnFormat($"Message with native ID `{messageId}` could not be moved to the error queue with additional headers because it was too large. Moving to the error queue as is.", e);

                await MoveToErrorQueueWithoutModification(cancellationToken).ConfigureAwait(false);
            }
        }

        void AssertVisibilityTimeout()
        {
            if (rawMessage.NextVisibleOn == null)
            {
                return;
            }

            var processingTime = timeProvider.GetElapsedTime(startTimestamp);
            var processedAtAccordingToServer = messageReceivedTime + processingTime;
            var visibleIn = rawMessage.NextVisibleOn.Value - processedAtAccordingToServer;
            if (visibleIn < TimeSpan.Zero)
            {
                throw new LeaseTimeoutException(rawMessage, visibilityTimeoutExceededBy: -visibleIn);
            }
        }

        /// <summary>
        /// Rejects the message requeueing it in the queue.
        /// </summary>
        public async Task Nack(CancellationToken cancellationToken = default)
        {
            AssertVisibilityTimeout();

            try
            {
                // the simplest solution to push the message back is to update its visibility timeout to 0 which is ok according to the API:
                // https://msdn.microsoft.com/en-us/library/azure/hh452234.aspx
                await inputQueue.UpdateMessageAsync(rawMessage.MessageId, rawMessage.PopReceipt, visibilityTimeout: TimeSpan.Zero, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch (RequestFailedException ex) when (ex.ErrorCode != QueueErrorCode.MessageNotFound)
            {
                throw;
            }
        }

        public static implicit operator QueueMessage(MessageRetrieved messageRetrieved) => messageRetrieved.rawMessage;

        BinaryData ReWrap(MessageWrapper wrapper)
        {
            string base64String = MessageWrapperHelper.ConvertToBase64String(wrapper, serializer);
            return BinaryData.FromString(base64String);
        }

        readonly QueueClient inputQueue;
        readonly QueueMessage rawMessage;
        readonly QueueClient errorQueue;
        readonly IMessageEnvelopeUnwrapper unwrapper;
        readonly MessageWrapperSerializer serializer;
        readonly DateTimeOffset messageReceivedTime;
        readonly long startTimestamp;
        readonly TimeProvider timeProvider;
        MessageWrapper unwrappedMessage;
        static ILog Logger = LogManager.GetLogger<MessageRetrieved>();
    }

    sealed class LeaseTimeoutException(QueueMessage rawMessage, TimeSpan visibilityTimeoutExceededBy)
        : Exception($"The pop receipt of the cloud queue message '{rawMessage.MessageId}' is invalid as it exceeded the next visible time by '{visibilityTimeoutExceededBy}'.");
}
