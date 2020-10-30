namespace NServiceBus.AzureStorageQueues
{
    using System;
    using Transport;

    internal static class IOutgoingTransportOperationExtensions
    {
        /// <summary>
        /// Gets the operation intent from the headers.
        /// </summary>
        /// <param name="operation">The operation.</param>
        /// <returns>The operation intent.</returns>
        public static MessageIntentEnum GetMessageIntent(this IOutgoingTransportOperation operation)
        {
            var messageIntent = default(MessageIntentEnum);

            if (operation.Message.Headers.TryGetValue(Headers.MessageIntent, out var messageIntentString))
            {
                Enum.TryParse(messageIntentString, true, out messageIntent);
            }

            return messageIntent;
        }
    }
}