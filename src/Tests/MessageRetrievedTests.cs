namespace NServiceBus.Transport.AzureStorageQueues.Tests
{
    using System;
    using System.Threading.Tasks;
    using global::Azure.Storage.Queues.Models;
    using NUnit.Framework;

    [TestFixture]
    public class MessageRetrievedTests
    {
        [Test]
        public void Ack_WhenExpired_ThrowsLeaseTimeout()
        {
            var message = QueuesModelFactory.QueueMessage("MessageId", "PopReceipt", "", 1, nextVisibleOn: DateTimeOffset.UtcNow.Subtract(TimeSpan.FromSeconds(30)));

            var messageRetrieved = new MessageRetrieved(null, null, message, DateTimeOffset.UtcNow, null, null);

            Assert.ThrowsAsync<LeaseTimeoutException>(async () => await messageRetrieved.Ack());
        }

        [Test]
        public async Task Ack_WhenProcessingTakesLongerThanLease_ThrowsLeaseTimeout()
        {
            var message = QueuesModelFactory.QueueMessage("MessageId", "PopReceipt", "", 1, nextVisibleOn: DateTimeOffset.UtcNow.Add(TimeSpan.FromMilliseconds(5)));

            var messageRetrieved = new MessageRetrieved(null, null, message, DateTimeOffset.UtcNow, null, null);

            await Task.Delay(10);

            Assert.ThrowsAsync<LeaseTimeoutException>(async () => await messageRetrieved.Ack());
        }

        [Test]
        public void Nack_WhenExpired_ThrowsLeaseTimeout()
        {
            var message = QueuesModelFactory.QueueMessage("MessageId", "PopReceipt", "", 1, nextVisibleOn: DateTimeOffset.UtcNow.Subtract(TimeSpan.FromSeconds(30)));

            var messageRetrieved = new MessageRetrieved(null, null, message, DateTimeOffset.UtcNow, null, null);

            Assert.ThrowsAsync<LeaseTimeoutException>(async () => await messageRetrieved.Nack());
        }

        [Test]
        public async Task Nack_WhenProcessingTakesLongerThanLease_ThrowsLeaseTimeout()
        {
            var message = QueuesModelFactory.QueueMessage("MessageId", "PopReceipt", "", 1, nextVisibleOn: DateTimeOffset.UtcNow.Add(TimeSpan.FromMilliseconds(5)));

            var messageRetrieved = new MessageRetrieved(null, null, message, DateTimeOffset.UtcNow, null, null);

            await Task.Delay(10);

            Assert.ThrowsAsync<LeaseTimeoutException>(async () => await messageRetrieved.Nack());
        }
    }
}