namespace NServiceBus.Azure.QuickTests
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using NServiceBus.Azure.Transports.WindowsAzureStorageQueues;
    using NUnit.Framework;

    public class When_serializing_message_wrapper
    {
        [TestCaseSource(nameof(GetCorev5SerializationTestCase))]
        public void Serialization_should_be_backward_compatible_with_corev5_serializer(MemoryStream stream, MessageWrapperSerializer serializer)
        {
            var message = Stringify(stream);
            var wrapper = serializer.Deserialize(stream);

            // serialize back
            stream.SetLength(0);
            serializer.Serialize(wrapper, stream);
            stream.Seek(0, SeekOrigin.Begin);

            var serialized = Stringify(stream);

            Assert.AreEqual(message, serialized);
        }

        // ReSharper disable once UnusedMethodReturnValue.Global
        public IEnumerable<ITestCaseData> GetCorev5SerializationTestCase()
        {
            yield return new TestCaseData(GetContent("MessageWrapper.v6.xml"), MessageWrapperSerializer.Xml.Value).SetName("Xml");
            yield return new TestCaseData(GetContent("MessageWrapper.v6.json"), MessageWrapperSerializer.Json.Value).SetName("JSON");
        }

        private static MemoryStream GetContent(string dataFileName)
        {
            var asm = typeof(When_serializing_message_wrapper).Assembly;
            var name = asm.GetManifestResourceNames().Single(n => n.Contains(dataFileName));
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