namespace NServiceBus.Azure.Transports.WindowsAzureStorageQueues
{
    using System;

    internal class MessageRetrieved
    {
        public MessageRetrieved(MessageWrapper wrapper, Action completeProcessing)
        {
            Wrapper = wrapper;
            CompleteProcessing = completeProcessing;
        }

        public MessageWrapper Wrapper { get; }
        public Action CompleteProcessing { get; }
    }
}