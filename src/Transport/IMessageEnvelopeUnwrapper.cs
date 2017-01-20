namespace NServiceBus.AzureStorageQueues
{
    using Azure.Transports.WindowsAzureStorageQueues;
    using Microsoft.WindowsAzure.Storage.Queue;

    interface IMessageEnvelopeUnwrapper
    {
        MessageWrapper Unwrap(CloudQueueMessage rawMessage);
    }
}