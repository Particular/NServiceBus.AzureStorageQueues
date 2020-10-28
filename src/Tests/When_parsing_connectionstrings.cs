using System;
using NServiceBus.Transport.AzureStorageQueues;
using NUnit.Framework;

[TestFixture]
[Category("Azure")]
public class When_parsing_queueaddress
{
    [Test]
    public void Should_parse_queue_name_and_alias()
    {
        const string queueAddressAsString = "myqueue@alias";
        var q = QueueAddress.Parse(queueAddressAsString);

        Assert.AreEqual(q.QueueName, "myqueue");
        Assert.AreEqual(q.Alias, "alias");
    }

    [Test]
    public void Should_throw_if_contains_connectionstring()
    {
        const string queue = "myqueue@DefaultEndpointsProtocol=https;AccountName=nservicebus;AccountKey=4CBm0byd405DrwMlNGQcHntKDgAQCjaxHNX4mmjMx0p3mNaxrg4Y9zdTVVy0MBzKjQtRKd1M6DF5CwQseBTw/g==";

        Assert.Throws<Exception>(() => QueueAddress.Parse(queue));
    }

    [TestCase("@accountName")]
    [TestCase("  @accountName")]
    [TestCase(default(string))]
    public void Should_not_parse_whitespace_queue_name(string name)
    {
        Assert.IsFalse(QueueAddress.TryParse(name, out var queue));
        Assert.IsFalse(queue.HasValue);
    }
}
