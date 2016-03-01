﻿namespace NServiceBus.Azure.QuickTests
{
    using NServiceBus.Azure.Transports.WindowsAzureStorageQueues;
    using NServiceBus.Azure.Transports.WindowsAzureStorageQueues.Config;
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

        [Test]
        public void Should_parse_queueindex_from_queuename_using_underscores() // azure queuestorage transport will replace dots by underscores
        {
            const string connectionstring = "myqueue_1";

            var index = QueueIndividualizer.ParseIndexFrom(connectionstring);

            Assert.AreEqual(index, 1);
        }
    }
}