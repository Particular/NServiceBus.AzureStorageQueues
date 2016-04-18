namespace NServiceBus.Azure.Transports.WindowsAzureStorageQueues
{
    using System;
    using System.IO;
    using System.Runtime.Serialization;
    using Microsoft.WindowsAzure.Storage.Queue;

    class MessageEnvelopeUnwrapper
    {
        public MessageEnvelopeUnwrapper(MessageWrapperSerializer messageSerializer)
        {
            messageWrapperSerializer = messageSerializer;
        }

        public MessageWrapper Unwrap(CloudQueueMessage rawMessage)
        {
            MessageWrapper m;
            using (var stream = new MemoryStream(rawMessage.AsBytes))
            {
                try
                {
                    m = messageWrapperSerializer.Deserialize(stream);
                }
                catch (Exception)
                {
                    throw new SerializationException("Failed to deserialize message with id: " + rawMessage.Id);
                }
            }

            if (m == null)
            {
                throw new SerializationException("Failed to deserialize message with id: " + rawMessage.Id);
            }

            if (m.ReplyToAddress != null)
            {
                m.Headers[Headers.ReplyToAddress] = m.ReplyToAddress;
            }
            m.Headers[Headers.CorrelationId] = m.CorrelationId;

            if (m.TimeToBeReceived != TimeSpan.MaxValue)
            {
                m.Headers[Headers.TimeToBeReceived] = m.TimeToBeReceived.ToString();
            }
            m.Headers[Headers.MessageIntent] = m.MessageIntent.ToString(); // message intent exztension method

            return m;
        }

        MessageWrapperSerializer messageWrapperSerializer;
    }
}