namespace NServiceBus.Transport.AzureStorageQueues.Tests
{
    using System;
    using NServiceBus.Transport.AzureStorageQueues;
    using NUnit.Framework;

    [TestFixture]
    public class When_parsing_queueaddress
    {
        [Test]
        public void Should_parse_queue_name_and_alias()
        {
            const string queueAddressAsString = "myqueue@alias";
            var q = QueueAddress.Parse(queueAddressAsString);

            Assert.Multiple(() =>
            {
                Assert.That(q.QueueName, Is.EqualTo("myqueue"));
                Assert.That(q.Alias, Is.EqualTo("alias"));
            });
        }

        [Test]
        public void Should_throw_if_contains_connectionstring()
        {
            const string queue = "myqueue@DefaultEndpointsProtocol=https;AccountName=nservicebus;AccountKey=4CBm0byd405DrwMlNGQcHntKDgAQCjaxHNX4mmjMx0p3mNaxrg4Y9zdTVVy0MBzKjQtRKd1M6DF5CwQseBTw/g==";

            Assert.Throws<FormatException>(() => QueueAddress.Parse(queue));
        }

        [Test]
        public void Should_parse_a_connectionstring_when_instructed_to_support_backward_compatibility()
        {
            const string queueName = "myqueue";
            const string connectionString = "DefaultEndpointsProtocol=https;AccountName=nservicebus;AccountKey=4CBm0byd405DrwMlNGQcHntKDgAQCjaxHNX4mmjMx0p3mNaxrg4Y9zdTVVy0MBzKjQtRKd1M6DF5CwQseBTw/g==";
            var queue = $"{queueName}@{connectionString}";

            var address = QueueAddress.Parse(queue, true);

            Assert.Multiple(() =>
            {
                Assert.That(address.QueueName, Is.EqualTo(queueName));
                Assert.That(address.Alias, Is.EqualTo(connectionString));
            });
        }

        [TestCase("@accountName")]
        [TestCase("  @accountName")]
        [TestCase(default(string))]
        public void Should_not_parse_whitespace_queue_name(string name)
        {
            Assert.Multiple(() =>
            {
                Assert.That(QueueAddress.TryParse(name, false, out var queue), Is.False);
                Assert.That(queue.HasValue, Is.False);
            });
        }
    }
}