namespace NServiceBus.Transport.AzureStorageQueues
{
    using System;
    using Azure.Transports.WindowsAzureStorageQueues;
    using Microsoft.WindowsAzure.Storage.Queue;

    class UserProvidedEnvelopeUnwrapper : IMessageEnvelopeUnwrapper
    {
        public UserProvidedEnvelopeUnwrapper(Func<CloudQueueMessage, MessageWrapper> unwrapper)
        {
            this.unwrapper = unwrapper;
        }

        public MessageWrapper Unwrap(CloudQueueMessage rawMessage)
        {

            return unwrapper(rawMessage);
        }

        Func<CloudQueueMessage, MessageWrapper> unwrapper;
    }
}