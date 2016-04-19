namespace NServiceBus.AzureStorageQueues
{
    using System;
    using System.IO;
    using System.Linq;
    using Azure.Transports.WindowsAzureStorageQueues;
    using MessageInterfaces;
    using MessageInterfaces.MessageMapper.Reflection;
    using Serialization;

    class MessageWrapperSerializer
    {
        public MessageWrapperSerializer(Func<IMessageMapper, IMessageSerializer> builder)
        {
            var mapper = new MessageMapper();
            mapper.Initialize(MessageTypes);
            messageSerializer = builder(mapper);
        }

        public void Serialize(MessageWrapper wrapper, Stream stream)
        {
            messageSerializer.Serialize(wrapper, stream);
        }

        public MessageWrapper Deserialize(Stream stream)
        {
            return messageSerializer.Deserialize(stream, MessageTypes).SingleOrDefault() as MessageWrapper;
        }

        readonly IMessageSerializer messageSerializer;

        static readonly Type[] MessageTypes =
        {
            typeof(MessageWrapper)
        };
    }
}