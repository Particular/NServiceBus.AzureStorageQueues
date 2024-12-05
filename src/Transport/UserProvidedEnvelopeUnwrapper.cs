namespace NServiceBus.Transport.AzureStorageQueues
{
    using System;
    using System.Buffers;
    using System.Text;
    using System.Text.Json;
    using Azure.Transports.WindowsAzureStorageQueues;
    using global::Azure.Storage.Queues.Models;

    class UserProvidedEnvelopeUnwrapper : IMessageEnvelopeUnwrapper
    {
        public UserProvidedEnvelopeUnwrapper(Func<QueueMessage, MessageWrapper> unwrapper)
        {
            this.unwrapper = unwrapper;
            arrayPool = ArrayPool<byte>.Shared;
        }

        public MessageWrapper Unwrap(QueueMessage rawMessage)
        {
            byte[] buffer = null;
            try
            {
                buffer = arrayPool.Rent(Encoding.UTF8.GetMaxByteCount(rawMessage.MessageText.Length));
                if (Convert.TryFromBase64String(rawMessage.MessageText, buffer, out int writtenByteCount))
                {
                    var wrapper = JsonSerializer.Deserialize<MessageWrapper>(buffer.AsMemory(0, writtenByteCount).Span);

                    if (wrapper?.Id != null)
                    {
                        //It's already an NServiceBus MessageWrapper (i.e. a delayedmessage wrapped version of the original unwrapped message)
                        return wrapper;
                    }
                }
                return unwrapper(rawMessage);
            }
            finally
            {
                if (buffer != null)
                {
                    arrayPool.Return(buffer);
                }
            }
        }

        Func<QueueMessage, MessageWrapper> unwrapper;
        ArrayPool<byte> arrayPool;
    }
}