namespace NServiceBus.Transport.AzureStorageQueues
{
    using System;
    using System.Runtime.Serialization;
    using Azure.Transports.WindowsAzureStorageQueues;
    using global::Azure.Storage.Queues.Models;
    using Logging;

    class DefaultMessageEnvelopeUnwrapper : IMessageEnvelopeUnwrapper
    {
        public DefaultMessageEnvelopeUnwrapper(MessageWrapperSerializer messageSerializer)
        {
            messageWrapperSerializer = messageSerializer;
        }

        public MessageWrapper Unwrap(QueueMessage rawMessage)
        {
            Logger.DebugFormat("Unwrapping native message (native ID: '{0}')", rawMessage.MessageId);

            var bytes = Convert.FromBase64String(rawMessage.MessageText);
            var m = messageWrapperSerializer.Deserialize(bytes);

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

        static ILog Logger = LogManager.GetLogger<DefaultMessageEnvelopeUnwrapper>();
    }
}