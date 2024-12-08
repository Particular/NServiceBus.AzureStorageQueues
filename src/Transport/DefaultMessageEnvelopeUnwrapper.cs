namespace NServiceBus.Transport.AzureStorageQueues
{
    using System;
    using System.Buffers.Text;
    using System.Buffers;
    using System.Runtime.Serialization;
    using System.Text.Json;
    using Azure.Transports.WindowsAzureStorageQueues;
    using global::Azure.Storage.Queues.Models;
    using Logging;

    class DefaultMessageEnvelopeUnwrapper : IMessageEnvelopeUnwrapper
    {
        public DefaultMessageEnvelopeUnwrapper(MessageWrapperSerializer messageSerializer)
        {
            messageWrapperSerializer = messageSerializer;
            arrayPool = ArrayPool<byte>.Shared;
        }

        public MessageWrapper Unwrap(QueueMessage rawMessage)
        {
            Logger.DebugFormat("Unwrapping native message (native ID: '{0}')", rawMessage.MessageId);

            byte[] buffer = null;
            try
            {
                if (Base64.IsValid(rawMessage.MessageText, out int requiredByteCount))
                {
                    buffer = arrayPool.Rent(requiredByteCount);
                    if (Convert.TryFromBase64String(rawMessage.MessageText, buffer, out int writtenByteCount))
                    {
                        var m = messageWrapperSerializer.Deserialize(buffer.AsMemory(0, writtenByteCount).ToArray());

                        if (m != null)
                        {
                            if (m.ReplyToAddress != null)
                            {
                                m.Headers[Headers.ReplyToAddress] = m.ReplyToAddress;
                            }
                            m.Headers[Headers.CorrelationId] = m.CorrelationId;
                            m.Headers[Headers.MessageIntent] = m.MessageIntent.ToString(); // message intent extension method

                            return m;
                        }
                    }
                }

                throw new SerializationException("Message cannot be unwrapped");
            }
            finally
            {
                if (buffer != null)
                {
                    arrayPool.Return(buffer);
                }
            }

        }

        MessageWrapperSerializer messageWrapperSerializer;
        ArrayPool<byte> arrayPool;
        static ILog Logger = LogManager.GetLogger<DefaultMessageEnvelopeUnwrapper>();
    }
}