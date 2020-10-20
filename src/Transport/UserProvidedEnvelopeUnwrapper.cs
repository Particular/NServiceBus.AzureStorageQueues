namespace NServiceBus.Transport.AzureStorageQueues
{
    using System;
    using Azure.Transports.WindowsAzureStorageQueues;
    using global::Azure.Storage.Queues.Models;

    class UserProvidedEnvelopeUnwrapper : IMessageEnvelopeUnwrapper
    {
        public UserProvidedEnvelopeUnwrapper(Func<QueueMessage, MessageWrapper> unwrapper)
        {
            this.unwrapper = unwrapper;
        }

        public MessageWrapper Unwrap(QueueMessage rawMessage)
        {

            return unwrapper(rawMessage);
        }

        Func<QueueMessage, MessageWrapper> unwrapper;
    }
}