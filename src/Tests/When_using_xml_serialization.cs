namespace NServiceBus.Azure.QuickTests
{
    using System.IO;
    using System.Linq;
    using System.Text;
    using NServiceBus.Azure.Transports.WindowsAzureStorageQueues;
    using NUnit.Framework;

    public class When_using_xml_serialization
    {
        [Test]
        public void Serializing_from_corev5_and_serializing_back_should_provide_equal_messages()
        {
            var stream = GetContent();
            var message = Stringify(stream);

            var serializer = MessageWrapperSerializer.Xml.Value;

            var wrapper = serializer.Deserialize(stream);

            // serialize back
            stream.SetLength(0);
            serializer.Serialize(wrapper, stream);
            stream.Seek(0, SeekOrigin.Begin);

            var serialized = Stringify(stream);

            Assert.AreEqual(message, serialized);
        }

        private static MemoryStream GetContent()
        {
            var asm = typeof(When_using_xml_serialization).Assembly;
            var name = asm.GetManifestResourceNames().Single(n => n.Contains("MessageWrapper.v6.xml"));
            var stream = new MemoryStream();
            using (var resource = asm.GetManifestResourceStream(name))
            {
                resource.CopyTo(stream);
            }

            stream.Seek(0, SeekOrigin.Begin);
            return stream;
        }

        private static string Stringify(Stream stream)
        {
            var position = stream.Position;

            try
            {
                return new StreamReader(stream, new UTF8Encoding(false), false, 1024, true).ReadToEnd();
            }
            finally
            {
                stream.Seek(position, SeekOrigin.Begin);
            }
        }
    }
}