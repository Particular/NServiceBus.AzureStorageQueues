namespace NServiceBus.Transport.AzureStorageQueues
{
    using Azure.Transports.WindowsAzureStorageQueues;
    using global::Azure.Storage.Queues.Models;

    interface IMessageEnvelopeUnwrapper
    {
        MessageWrapper Unwrap(QueueMessage rawMessage);
    }
}