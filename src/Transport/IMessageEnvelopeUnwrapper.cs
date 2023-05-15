namespace NServiceBus.Transport.AzureStorageQueues
{
    using System;
    using Azure.Transports.WindowsAzureStorageQueues;
    using global::Azure.Storage.Queues.Models;

    interface IMessageEnvelopeUnwrapper
    {
        MessageWrapper Unwrap(QueueMessage rawMessage);
        BinaryData ReWrap(MessageWrapper wrapper);
    }
}