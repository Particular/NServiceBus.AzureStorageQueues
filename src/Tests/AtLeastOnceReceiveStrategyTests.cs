namespace NServiceBus.Transport.AzureStorageQueues.Tests
{
    using System;
    using System.Threading.Tasks;
    using Azure.Transports.WindowsAzureStorageQueues;
    using global::Azure;
    using global::Azure.Storage.Queues.Models;
    using NUnit.Framework;

    [TestFixture]
    public class AtLeastOnceReceiveStrategyTests
    {
        [Test]
        public async Task Should_complete_message_on_next_receive_when_pipeline_successful_but_completion_failed_due_to_expired_lease()
        {
            var fakeQueueClient = new FakeQueueClient();
            var onMessageCalled = 0;
            var onErrorCalled = 0;

            var receiveStrategy = new AtLeastOnceReceiveStrategy((_, _) =>
            {
                onMessageCalled++;
                return Task.CompletedTask;
            }, (_, _) =>
            {
                onErrorCalled++;
                return Task.FromResult(ErrorHandleResult.Handled);
            }, (_, _, _) => { });

            var messageId = Guid.NewGuid().ToString();

            var rawMessageThatIsExpired = QueuesModelFactory.QueueMessage("RawMessageId1", "PopReceipt1", "", 1, nextVisibleOn: DateTimeOffset.UtcNow.Subtract(TimeSpan.FromSeconds(30)));
            var messageRetrieved1 = new MessageRetrieved(null, null, rawMessageThatIsExpired, fakeQueueClient, null, DateTimeOffset.UtcNow, TimeProvider.System);
            var messageWrapper1 = new MessageWrapper { Id = messageId, Headers = [] };

            await receiveStrategy.Receive(messageRetrieved1, messageWrapper1, "queue");

            var rawMessageThatIsValid = QueuesModelFactory.QueueMessage("RawMessageId2", "PopReceipt2", "", 1, nextVisibleOn: DateTimeOffset.UtcNow.Add(TimeSpan.FromSeconds(30)));
            var messageRetrieved2 = new MessageRetrieved(null, null, rawMessageThatIsValid, fakeQueueClient, null, DateTimeOffset.UtcNow, TimeProvider.System);
            var messageWrapper2 = new MessageWrapper { Id = messageId, Headers = [] };

            await receiveStrategy.Receive(messageRetrieved2, messageWrapper2, "queue");

            Assert.Multiple(() =>
            {
                Assert.That(fakeQueueClient.DeletedMessages, Has.Count.EqualTo(1).And.Contains(("RawMessageId2", "PopReceipt2")));
                Assert.That(onMessageCalled, Is.EqualTo(1));
                Assert.That(onErrorCalled, Is.Zero);
            });
        }

        [Test]
        public async Task Should_rethrow_on_next_receive_when_message_could_not_be_completed()
        {
            var fakeQueueClient = new FakeQueueClient();

            var receiveStrategy = new AtLeastOnceReceiveStrategy((_, _) => Task.CompletedTask, (_, _) => Task.FromResult(ErrorHandleResult.Handled), (_, _, _) => { });

            var messageId = Guid.NewGuid().ToString();

            var rawMessageThatIsExpired = QueuesModelFactory.QueueMessage("RawMessageId1", "PopReceipt1", "", 1, nextVisibleOn: DateTimeOffset.UtcNow.Subtract(TimeSpan.FromSeconds(30)));
            var messageRetrieved1 = new MessageRetrieved(null, null, rawMessageThatIsExpired, fakeQueueClient, null, DateTimeOffset.UtcNow, TimeProvider.System);
            var messageWrapper1 = new MessageWrapper { Id = messageId, Headers = [] };

            await receiveStrategy.Receive(messageRetrieved1, messageWrapper1, "queue");

            fakeQueueClient.DeleteMessageCallback = () => throw new RequestFailedException(404, "MessageNotFound", QueueErrorCode.MessageNotFound.ToString(), null);

            var rawMessageThatIsValid = QueuesModelFactory.QueueMessage("RawMessageId2", "PopReceipt2", "", 1, nextVisibleOn: DateTimeOffset.UtcNow.Add(TimeSpan.FromSeconds(30)));
            var messageRetrieved2 = new MessageRetrieved(null, null, rawMessageThatIsValid, fakeQueueClient, null, DateTimeOffset.UtcNow, TimeProvider.System);
            var messageWrapper2 = new MessageWrapper { Id = messageId, Headers = [] };

            Assert.ThrowsAsync<LeaseTimeoutException>(async () => await receiveStrategy.Receive(messageRetrieved2, messageWrapper2, "queue"));
            Assert.That(fakeQueueClient.DeletedMessages, Is.Empty);
        }

        [Test]
        public async Task Should_complete_message_on_next_receive_when_error_pipeline_successful_but_completion_failed_due_to_expired_lease()
        {
            var fakeQueueClient = new FakeQueueClient();

            var onMessageCalled = 0;
            var onErrorCalled = 0;

            var receiveStrategy = new AtLeastOnceReceiveStrategy((_, _) =>
            {
                onMessageCalled++;
                return Task.FromException<InvalidOperationException>(new InvalidOperationException());
            }, (_, _) =>
            {
                onErrorCalled++;
                return Task.FromResult(ErrorHandleResult.Handled);
            }, (_, _, _) => { });

            var messageId = Guid.NewGuid().ToString();

            var rawMessageThatIsExpired = QueuesModelFactory.QueueMessage("RawMessageId1", "PopReceipt1", "", 1, nextVisibleOn: DateTimeOffset.UtcNow.Subtract(TimeSpan.FromSeconds(30)));
            var messageRetrieved1 = new MessageRetrieved(null, null, rawMessageThatIsExpired, fakeQueueClient, null, DateTimeOffset.UtcNow, TimeProvider.System);
            var messageWrapper1 = new MessageWrapper { Id = messageId, Headers = [] };

            await receiveStrategy.Receive(messageRetrieved1, messageWrapper1, "queue");

            var rawMessageThatIsValid = QueuesModelFactory.QueueMessage("RawMessageId2", "PopReceipt2", "", 1, nextVisibleOn: DateTimeOffset.UtcNow.Add(TimeSpan.FromSeconds(30)));
            var messageRetrieved2 = new MessageRetrieved(null, null, rawMessageThatIsValid, fakeQueueClient, null, DateTimeOffset.UtcNow, TimeProvider.System);
            var messageWrapper2 = new MessageWrapper { Id = messageId, Headers = [] };

            await receiveStrategy.Receive(messageRetrieved2, messageWrapper2, "queue");

            Assert.Multiple(() =>
            {
                Assert.That(fakeQueueClient.DeletedMessages, Has.Count.EqualTo(1).And.Contains(("RawMessageId2", "PopReceipt2")));
                Assert.That(onMessageCalled, Is.EqualTo(1));
                Assert.That(onErrorCalled, Is.EqualTo(1));
            });
        }
    }
}