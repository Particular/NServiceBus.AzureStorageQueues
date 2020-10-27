using System;
using NServiceBus.Transport;

namespace NServiceBus.AzureStorageQueues
{
    internal static class UnicastTransportOperationExtensions
    {
        /// <summary>
        /// Gets the operation intent from the headers.
        /// </summary>
        /// <param name="operation">The operation.</param>
        /// <returns>The operation intent.</returns>
        public static MessageIntentEnum GetMessageIntent(this UnicastTransportOperation operation)
        {
            Guard.AgainstNull(nameof(operation), operation);
            var messageIntent = default(MessageIntentEnum);

            if (operation.Message.Headers.TryGetValue(Headers.MessageIntent, out var messageIntentString))
            {
                Enum.TryParse(messageIntentString, true, out messageIntent);
            }

            return messageIntent;
        }
    }
}