namespace NServiceBus.AzureStorageQueues
{
    using Azure.Transports.WindowsAzureStorageQueues;
    using Microsoft.WindowsAzure.Storage.Queue;

    /// <summary>
    /// Extracts a native cloud queue message to the internal message wrapper format.
    /// </summary>
    public interface IMessageEnvelopeUnwrapper
    {
        /// <summary>
        /// Unwraps the given message.
        /// </summary>
        /// <param name="rawMessage">The raw cloud queue message received from the Azure storage queue.</param>
        /// <returns></returns>
        MessageWrapper Unwrap(CloudQueueMessage rawMessage);
    }
}