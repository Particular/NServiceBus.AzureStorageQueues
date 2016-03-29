namespace NServiceBus.AzureStorageQueues
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.Serialization;
    using System.Text;
    using System.Xml;
    using Newtonsoft.Json;
    using NServiceBus.Serialization;
    using Formatting = System.Xml.Formatting;
    using JSON = JsonSerializer;

    public class MessageWrapperSerializer
    {
        public static readonly Lazy<MessageWrapperSerializer> Json = new Lazy<MessageWrapperSerializer>(BuildJsonMessageWrapperSerializer);
        public static readonly Lazy<MessageWrapperSerializer> Xml = new Lazy<MessageWrapperSerializer>(BuildXmlMessageWrapperSerializer);

        private static readonly Dictionary<string, string> CoreV5XmlToDataContractSerializer =
            new Dictionary<string, string>
            {
                {"<NServiceBus.KeyValuePairOfStringAndString>", "<NServiceBus.KeyValuePairOfStringAndString xmlns=\"\">"},
                {
                    "<?xml version=\"1.0\" ?>\r\n<MessageWrapper xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\"",
                    "<MessageWrapper xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\""
                }
            };

        static readonly UTF8Encoding UTF8NoBOM =
            new UTF8Encoding(false, true);

        private readonly Func<Stream, MessageWrapper> deserialize;
        private readonly Action<MessageWrapper, Stream> serialize;

        public MessageWrapperSerializer(Action<MessageWrapper, Stream> serialize, Func<Stream, MessageWrapper> deserialize)
        {
            if (serialize == null)
            {
                throw new ArgumentNullException(nameof(serialize));
            }

            if (deserialize == null)
            {
                throw new ArgumentNullException(nameof(deserialize));
            }

            this.serialize = serialize;
            this.deserialize = deserialize;
        }

        internal static MessageWrapperSerializer TryBuild(SerializationDefinition definition, Func<SerializationDefinition, MessageWrapperSerializer> factory)
        {
            if (factory != null)
            {
                return factory(definition);
            }

            if (definition is XmlSerializer)
            {
                return Xml.Value;
            }

            if (definition is JSON)
            {
                return Json.Value;
            }
            return null;
        }

        static MessageWrapperSerializer BuildXmlMessageWrapperSerializer()
        {
            var serializer = new DataContractSerializer(typeof(MessageWrapper));
            Action<MessageWrapper, Stream> serialize = (wrapper, stream) =>
            {
                var sw = new StringWriter();
                string content;
                using (var writer = new XmlTextWriter(sw))
                {
                    writer.Formatting = Formatting.Indented;
                    serializer.WriteObject(writer, wrapper);
                    writer.Flush();
                    content = sw.ToString();
                }

                foreach (var core2dataContract in CoreV5XmlToDataContractSerializer)
                {
                    content = content.Replace(core2dataContract.Value, core2dataContract.Key);
                }

                using (var streamWriter = WriterFrom(stream))
                {
                    streamWriter.Write(content);
                    streamWriter.Flush();
                }
            };

            Func<Stream, MessageWrapper> deserialize = stream =>
            {
                using (var sr = ReaderFrom(stream))
                {
                    var content = sr.ReadToEnd();

                    foreach (var core2dataContract in CoreV5XmlToDataContractSerializer)
                    {
                        content = content.Replace(core2dataContract.Key, core2dataContract.Value);
                    }

                    using (var reader = new XmlTextReader(new StringReader(content)))
                    {
                        return (MessageWrapper) serializer.ReadObject(reader);
                    }
                }
            };

            return new MessageWrapperSerializer(serialize, deserialize);
        }

        static MessageWrapperSerializer BuildJsonMessageWrapperSerializer()
        {
            var jsonSerializer = new JsonSerializer();
            var deserializeFunc = BuildJsonDeserialize(jsonSerializer);
            var serializeFunc = BuildJsonSerialize(jsonSerializer);

            return new MessageWrapperSerializer(serializeFunc, deserializeFunc);
        }

        public void Serialize(MessageWrapper wrapper, Stream stream)
        {
            serialize(wrapper, stream);
        }

        public MessageWrapper Deserialize(Stream stream)
        {
            return deserialize(stream);
        }

        private static Action<MessageWrapper, Stream> BuildJsonSerialize(JsonSerializer jsonSerializer)
        {
            Action<MessageWrapper, Stream> serializeFunc = (wrapper, stream) =>
            {
                using (var jsonWriter = new JsonTextWriter(WriterFrom(stream)))
                {
                    jsonSerializer.Serialize(jsonWriter, wrapper);
                    jsonWriter.Flush();
                }
            };
            return serializeFunc;
        }

        private static Func<Stream, MessageWrapper> BuildJsonDeserialize(JsonSerializer jsonSerializer)
        {
            Func<Stream, MessageWrapper> deserializeFunc = stream =>
            {
                using (var jsonReader = new JsonTextReader(ReaderFrom(stream)))
                {
                    return jsonSerializer.Deserialize<MessageWrapper>(jsonReader);
                }
            };
            return deserializeFunc;
        }

        private static StreamWriter WriterFrom(Stream stream)
        {
            return new StreamWriter(stream, UTF8NoBOM, 4096, true);
        }

        private static StreamReader ReaderFrom(Stream stream)
        {
            return new StreamReader(stream, UTF8NoBOM, false, 4096, true);
        }
    }
}