﻿namespace NServiceBus.Azure.QuickTests
{
    using NUnit.Framework;

    [TestFixture]
    [Category("Azure")]
    public class When_parsing_connectionstrings
    {
        [Test]
        public void Should_parse_queuename_from_azure_storage_connectionstring()
        {
            const string connectionstring = "myqueue@DefaultEndpointsProtocol=https;AccountName=nservicebus;AccountKey=4CBm0byd405DrwMlNGQcHntKDgAQCjaxHNX4mmjMx0p3mNaxrg4Y9zdTVVy0MBzKjQtRKd1M6DF5CwQseBTw/g==";

            var queueName = new Azure.Transports.WindowsAzureStorageQueues.ConnectionStringParser().ParseQueueNameFrom(connectionstring);

            Assert.AreEqual(queueName, "myqueue");
        }

        [Test]
        public void Should_parse_namespace_from_azure_storage_connectionstring()
        {
            const string connectionstring = "myqueue@DefaultEndpointsProtocol=https;AccountName=nservicebus;AccountKey=4CBm0byd405DrwMlNGQcHntKDgAQCjaxHNX4mmjMx0p3mNaxrg4Y9zdTVVy0MBzKjQtRKd1M6DF5CwQseBTw/g==";

            var @namespace = new Azure.Transports.WindowsAzureStorageQueues.ConnectionStringParser().ParseNamespaceFrom(connectionstring);

            Assert.AreEqual(@namespace, "DefaultEndpointsProtocol=https;AccountName=nservicebus;AccountKey=4CBm0byd405DrwMlNGQcHntKDgAQCjaxHNX4mmjMx0p3mNaxrg4Y9zdTVVy0MBzKjQtRKd1M6DF5CwQseBTw/g==");
        }

        [Test]
        public void Should_parse_queueindex_from_queuename_using_underscores() // azure queuestorage transport will replace dots by underscores
        {
            const string connectionstring = "myqueue_1";

            var index = new Azure.Transports.WindowsAzureStorageQueues.ConnectionStringParser().ParseIndexFrom(connectionstring);

            Assert.AreEqual(index, 1);
        }
    }
}