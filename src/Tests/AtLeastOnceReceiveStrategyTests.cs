namespace NServiceBus.Transport.AzureStorageQueues.Tests
{
    using System;
    using System.Collections.Generic;
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

            var receiveStrategy = new AtLeastOnceReceiveStrategy(messageContext =>
            {
                onMessageCalled++;
                return Task.CompletedTask;
            }, errorContext =>
            {
                onErrorCalled++;
                return Task.FromResult(ErrorHandleResult.Handled);
            }, new CriticalError(_ => Task.CompletedTask));

            var messageId = Guid.NewGuid().ToString();

            var rawMessageThatIsExpired = QueuesModelFactory.QueueMessage("RawMessageId1", "PopReceipt1", "", 1, nextVisibleOn: DateTimeOffset.UtcNow.Subtract(TimeSpan.FromSeconds(30)));
            var messageRetrieved1 = new MessageRetrieved(null, null, rawMessageThatIsExpired, fakeQueueClient, null);
            var messageWrapper1 = new MessageWrapper { Id = messageId, Headers = new Dictionary<string, string>() };

            Assert.ThrowsAsync<LeaseTimeoutException>(async () => await receiveStrategy.Receive(messageRetrieved1, messageWrapper1));

            var rawMessageThatIsValid = QueuesModelFactory.QueueMessage("RawMessageId2", "PopReceipt2", "", 1, nextVisibleOn: DateTimeOffset.UtcNow.Add(TimeSpan.FromSeconds(30)));
            var messageRetrieved2 = new MessageRetrieved(null, null, rawMessageThatIsValid, fakeQueueClient, null);
            var messageWrapper2 = new MessageWrapper { Id = messageId, Headers = new Dictionary<string, string>() };

            await receiveStrategy.Receive(messageRetrieved2, messageWrapper2);

            Assert.Multiple(() =>
            {
                Assert.That(fakeQueueClient.DeletedMessages, Has.Count.EqualTo(1).And.Contains(("RawMessageId2", "PopReceipt2")));
                Assert.That(onMessageCalled, Is.EqualTo(1));
                Assert.That(onErrorCalled, Is.Zero);
            });
        }

        [Test]
        public void Should_rethrow_on_next_receive_when_message_could_not_be_completed()
        {
            var fakeQueueClient = new FakeQueueClient();

            var receiveStrategy = new AtLeastOnceReceiveStrategy(messageContext => Task.CompletedTask, errorContext => Task.FromResult(ErrorHandleResult.Handled), new CriticalError(_ => Task.CompletedTask));

            var messageId = Guid.NewGuid().ToString();

            var rawMessageThatIsExpired = QueuesModelFactory.QueueMessage("RawMessageId1", "PopReceipt1", "", 1, nextVisibleOn: DateTimeOffset.UtcNow.Subtract(TimeSpan.FromSeconds(30)));
            var messageRetrieved1 = new MessageRetrieved(null, null, rawMessageThatIsExpired, fakeQueueClient, null);
            var messageWrapper1 = new MessageWrapper { Id = messageId, Headers = new Dictionary<string, string>() };

            Assert.ThrowsAsync<LeaseTimeoutException>(async () => await receiveStrategy.Receive(messageRetrieved1, messageWrapper1));

            fakeQueueClient.DeleteMessageCallback = () => throw new RequestFailedException(404, "MessageNotFound", QueueErrorCode.MessageNotFound.ToString(), null);

            var rawMessageThatIsValid = QueuesModelFactory.QueueMessage("RawMessageId2", "PopReceipt2", "", 1, nextVisibleOn: DateTimeOffset.UtcNow.Add(TimeSpan.FromSeconds(30)));
            var messageRetrieved2 = new MessageRetrieved(null, null, rawMessageThatIsValid, fakeQueueClient, null);
            var messageWrapper2 = new MessageWrapper { Id = messageId, Headers = new Dictionary<string, string>() };

            Assert.ThrowsAsync<LeaseTimeoutException>(async () => await receiveStrategy.Receive(messageRetrieved2, messageWrapper2));
            Assert.That(fakeQueueClient.DeletedMessages, Is.Empty);
        }

        [Test]
        public async Task Should_eventually_complete_the_message_even_when_next_receive_threw_when_message_could_not_be_completed()
        {
            var fakeQueueClient = new FakeQueueClient();

            var receiveStrategy = new AtLeastOnceReceiveStrategy(messageContext => Task.CompletedTask, errorContext => Task.FromResult(ErrorHandleResult.Handled), new CriticalError(_ => Task.CompletedTask));

            var messageId = Guid.NewGuid().ToString();

            var rawMessageThatIsExpired = QueuesModelFactory.QueueMessage("RawMessageId1", "PopReceipt1", "", 1, nextVisibleOn: DateTimeOffset.UtcNow.Subtract(TimeSpan.FromSeconds(30)));
            var messageRetrieved1 = new MessageRetrieved(null, null, rawMessageThatIsExpired, fakeQueueClient, null);
            var messageWrapper1 = new MessageWrapper { Id = messageId, Headers = new Dictionary<string, string>() };

            Assert.ThrowsAsync<LeaseTimeoutException>(async () => await receiveStrategy.Receive(messageRetrieved1, messageWrapper1));

            fakeQueueClient.DeleteMessageCallback = () => throw new RequestFailedException(404, "MessageNotFound", QueueErrorCode.MessageNotFound.ToString(), null);

            var rawMessageThatCannotBeCompleted = QueuesModelFactory.QueueMessage("RawMessageId2", "PopReceipt2", "", 1, nextVisibleOn: DateTimeOffset.UtcNow.Add(TimeSpan.FromSeconds(30)));
            var messageRetrieved2 = new MessageRetrieved(null, null, rawMessageThatCannotBeCompleted, fakeQueueClient, null);
            var messageWrapper2 = new MessageWrapper { Id = messageId, Headers = new Dictionary<string, string>() };

            Assert.ThrowsAsync<LeaseTimeoutException>(async () => await receiveStrategy.Receive(messageRetrieved2, messageWrapper2));

            fakeQueueClient.DeleteMessageCallback = () => Task.FromResult(default(Response));

            var rawMessageThatIsValid = QueuesModelFactory.QueueMessage("RawMessageId3", "PopReceipt3", "", 1, nextVisibleOn: DateTimeOffset.UtcNow.Add(TimeSpan.FromSeconds(30)));
            var messageRetrieved3 = new MessageRetrieved(null, null, rawMessageThatIsValid, fakeQueueClient, null);
            var messageWrapper3 = new MessageWrapper { Id = messageId, Headers = new Dictionary<string, string>() };

            await receiveStrategy.Receive(messageRetrieved3, messageWrapper3);

            Assert.That(fakeQueueClient.DeletedMessages, Has.Count.EqualTo(1).And.Contains(("RawMessageId3", "PopReceipt3")));
        }

        [Test]
        public async Task Should_complete_message_on_next_receive_when_error_pipeline_successful_but_completion_failed_due_to_expired_lease()
        {
            var fakeQueueClient = new FakeQueueClient();

            var onMessageCalled = 0;
            var onErrorCalled = 0;

            var receiveStrategy = new AtLeastOnceReceiveStrategy(messageContext =>
            {
                onMessageCalled++;
                return Task.FromException<InvalidOperationException>(new InvalidOperationException());
            }, errorContext =>
            {
                onErrorCalled++;
                return Task.FromResult(ErrorHandleResult.Handled);
            }, new CriticalError(_ => Task.CompletedTask));

            var messageId = Guid.NewGuid().ToString();

            var rawMessageThatIsExpired = QueuesModelFactory.QueueMessage("RawMessageId1", "PopReceipt1", "", 1, nextVisibleOn: DateTimeOffset.UtcNow.Subtract(TimeSpan.FromSeconds(30)));
            var messageRetrieved1 = new MessageRetrieved(null, null, rawMessageThatIsExpired, fakeQueueClient, null);
            var messageWrapper1 = new MessageWrapper { Id = messageId, Headers = new Dictionary<string, string>() };

            await receiveStrategy.Receive(messageRetrieved1, messageWrapper1);

            var rawMessageThatIsValid = QueuesModelFactory.QueueMessage("RawMessageId2", "PopReceipt2", "", 1, nextVisibleOn: DateTimeOffset.UtcNow.Add(TimeSpan.FromSeconds(30)));
            var messageRetrieved2 = new MessageRetrieved(null, null, rawMessageThatIsValid, fakeQueueClient, null);
            var messageWrapper2 = new MessageWrapper { Id = messageId, Headers = new Dictionary<string, string>() };

            await receiveStrategy.Receive(messageRetrieved2, messageWrapper2);

            Assert.Multiple(() =>
            {
                Assert.That(fakeQueueClient.DeletedMessages, Has.Count.EqualTo(1).And.Contains(("RawMessageId2", "PopReceipt2")));
                Assert.That(onMessageCalled, Is.EqualTo(1));
                Assert.That(onErrorCalled, Is.EqualTo(1));
            });
        }
    }
}