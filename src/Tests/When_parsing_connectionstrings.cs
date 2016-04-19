namespace NServiceBus.Azure.QuickTests
{
    using Azure.Transports.WindowsAzureStorageQueues;
    using Azure.Transports.WindowsAzureStorageQueues.Config;
    using NUnit.Framework;

    [TestFixture]
    [Category("Azure")]
    public class When_parsing_connectionstrings
    {
        [Test]
        public void Should_parse_queuename_from_azure_storage_connectionstring()
        {
            const string queue = "myqueue@DefaultEndpointsProtocol=https;AccountName=nservicebus;AccountKey=4CBm0byd405DrwMlNGQcHntKDgAQCjaxHNX4mmjMx0p3mNaxrg4Y9zdTVVy0MBzKjQtRKd1M6DF5CwQseBTw/g==";
            var q = QueueAddress.Parse(queue);

            Assert.AreEqual(q.QueueName, "myqueue");
        }

        [Test]
        public void Should_parse_namespace_from_azure_storage_connectionstring()
        {
            const string queue = "myqueue@DefaultEndpointsProtocol=https;AccountName=nservicebus;AccountKey=4CBm0byd405DrwMlNGQcHntKDgAQCjaxHNX4mmjMx0p3mNaxrg4Y9zdTVVy0MBzKjQtRKd1M6DF5CwQseBTw/g==";
            var q = QueueAddress.Parse(queue);

            Assert.AreEqual(q.StorageAccount, "DefaultEndpointsProtocol=https;AccountName=nservicebus;AccountKey=4CBm0byd405DrwMlNGQcHntKDgAQCjaxHNX4mmjMx0p3mNaxrg4Y9zdTVVy0MBzKjQtRKd1M6DF5CwQseBTw/g==");
        }

        [TestCase("@accountName")]
        [TestCase("  @accountName")]
        [TestCase(default(string))]
        public void Should_not_parse_whitespace_queue_name(string name)
        {
            QueueAddress queue;
            Assert.IsFalse(QueueAddress.TryParse(name, out queue));
            Assert.IsNull(queue);
        }
    }
}