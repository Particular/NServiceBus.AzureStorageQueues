namespace NServiceBus.Transport.AzureStorageQueues
{
    using System;
    using Transport;

    static class OutgoingTransportOperationExtensions
    {
        /// <summary>
        /// Gets the operation intent from the headers.
        /// </summary>
        /// <param name="operation">The operation.</param>
        /// <returns>The operation intent.</returns>
        public static MessageIntent GetMessageIntent(this IOutgoingTransportOperation operation)
        {
            var messageIntent = default(MessageIntent);

            if (operation.Message.Headers.TryGetValue(Headers.MessageIntent, out var messageIntentString))
            {
                Enum.TryParse(messageIntentString, true, out messageIntent);
            }

            return messageIntent;
        }
    }
}