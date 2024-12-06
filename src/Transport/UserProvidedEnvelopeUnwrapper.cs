namespace NServiceBus.Transport.AzureStorageQueues
{
    using System;
    using Azure.Transports.WindowsAzureStorageQueues;
    using global::Azure.Storage.Queues.Models;

    class UserProvidedEnvelopeUnwrapper : IMessageEnvelopeUnwrapper
    {
        public UserProvidedEnvelopeUnwrapper(Func<QueueMessage, MessageWrapper> unwrapper, DefaultMessageEnvelopeUnwrapper defaultUnwrapper)
        {
            this.unwrapper = unwrapper;
            this.defaultUnwrapper = defaultUnwrapper;
        }

        public MessageWrapper Unwrap(QueueMessage rawMessage) => unwrapper(rawMessage) ?? defaultUnwrapper.Unwrap(rawMessage);

        readonly Func<QueueMessage, MessageWrapper> unwrapper;
        readonly DefaultMessageEnvelopeUnwrapper defaultUnwrapper;
    }
}