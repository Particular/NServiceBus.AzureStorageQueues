namespace NServiceBus.Transport.AzureStorageQueues
{
    using System.IO;
    using System.Runtime.Serialization;
    using Azure.Transports.WindowsAzureStorageQueues;
    using Microsoft.WindowsAzure.Storage.Queue;

    class DefaultMessageEnvelopeUnwrapper : IMessageEnvelopeUnwrapper
    {
        public DefaultMessageEnvelopeUnwrapper(MessageWrapperSerializer messageSerializer)
        {
            messageWrapperSerializer = messageSerializer;
        }

        public MessageWrapper Unwrap(CloudQueueMessage rawMessage)
        {
            MessageWrapper m;
            using (var stream = new MemoryStream(rawMessage.AsBytes))
            {
                m = messageWrapperSerializer.Deserialize(stream);
            }

            if (m == null)
            {
                throw new SerializationException("Message is null");
            }

            if (m.ReplyToAddress != null)
            {
                m.Headers[Headers.ReplyToAddress] = m.ReplyToAddress;
            }
            m.Headers[Headers.CorrelationId] = m.CorrelationId;

            m.Headers[Headers.MessageIntent] = m.MessageIntent.ToString(); // message intent extension method

            return m;
        }

        MessageWrapperSerializer messageWrapperSerializer;
    }
}