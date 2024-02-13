namespace NServiceBus.Transport.AzureStorageQueues.Tests
{
    using System;
    using System.Collections.Generic;
    using global::Azure.Storage.Queues.Models;
    using NUnit.Framework;

    [TestFixture]
    public class MessageRetrievedTests
    {
        [Test]
        public void Ack_WhenExpired_ThrowsLeaseTimeout()
        {
            var message = QueuesModelFactory.QueueMessage("MessageId", "PopReceipt", "", 1, nextVisibleOn: DateTimeOffset.UtcNow.Subtract(TimeSpan.FromSeconds(30)));

            var messageRetrieved = new MessageRetrieved(null, null, message, null, null, DateTimeOffset.UtcNow, TimeProvider.System);

            Assert.ThrowsAsync<LeaseTimeoutException>(async () => await messageRetrieved.Ack());
        }

        [Test]
        public void Ack_WhenProcessingTakesLongerThanLease_ThrowsLeaseTimeout()
        {
            var fakeTimeProvider = new FakeTimeProvider();
            fakeTimeProvider.Timestamps.Enqueue(0);
            fakeTimeProvider.Timestamps.Enqueue(200 * fakeTimeProvider.TimestampFrequency);

            var message = QueuesModelFactory.QueueMessage("MessageId", "PopReceipt", "", 1, nextVisibleOn: DateTimeOffset.UtcNow.Add(TimeSpan.FromMilliseconds(5)));

            var messageRetrieved = new MessageRetrieved(null, null, message, null, null, DateTimeOffset.UtcNow, fakeTimeProvider);

            Assert.ThrowsAsync<LeaseTimeoutException>(async () => await messageRetrieved.Ack());
        }

        [Test]
        public void Nack_WhenExpired_ThrowsLeaseTimeout()
        {
            var message = QueuesModelFactory.QueueMessage("MessageId", "PopReceipt", "", 1, nextVisibleOn: DateTimeOffset.UtcNow.Subtract(TimeSpan.FromSeconds(30)));

            var messageRetrieved = new MessageRetrieved(null, null, message, null, null, DateTimeOffset.UtcNow, TimeProvider.System);

            Assert.ThrowsAsync<LeaseTimeoutException>(async () => await messageRetrieved.Nack());
        }

        [Test]
        public void Nack_WhenProcessingTakesLongerThanLease_ThrowsLeaseTimeout()
        {
            var fakeTimeProvider = new FakeTimeProvider();
            fakeTimeProvider.Timestamps.Enqueue(0);
            fakeTimeProvider.Timestamps.Enqueue(200 * fakeTimeProvider.TimestampFrequency);

            var message = QueuesModelFactory.QueueMessage("MessageId", "PopReceipt", "", 1, nextVisibleOn: DateTimeOffset.UtcNow.Add(TimeSpan.FromMilliseconds(5)));

            var messageRetrieved = new MessageRetrieved(null, null, message, null, null, DateTimeOffset.UtcNow, fakeTimeProvider);

            Assert.ThrowsAsync<LeaseTimeoutException>(async () => await messageRetrieved.Nack());
        }

        class FakeTimeProvider : TimeProvider
        {
            public Queue<long> Timestamps { get; } = new();
            public override long GetTimestamp() => Timestamps.TryDequeue(out var timeStamp) ? timeStamp : base.GetTimestamp();
        }
    }
}