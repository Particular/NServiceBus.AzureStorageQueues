namespace NServiceBus.Transport.AzureStorageQueues.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_message_is_sent_with_time_to_be_received_set_to_more_than_30_days : NServiceBusAcceptanceTest
    {
        [Test]
        public Task Should_throw_exception()
        {
            return Scenario.Define<Context>()
                .WithEndpoint<ReceiverEndPoint>(b => b.When((bus, c) =>
                {
                    var exception = Assert.ThrowsAsync<InvalidOperationException>(() => bus.SendLocal(new MessageNotToBeSent()));
                    var expectedMessage = $"TimeToBeReceived is set to more than 30 days for message type '{typeof(MessageNotToBeSent).FullName}'.";
                    Assert.AreEqual(expectedMessage, exception.Message);
                    c.ExceptionReceived = true;

                    return Task.FromResult(0);
                }))
                .Done(c => c.ExceptionReceived)
                .Run();
        }

        public class Context : ScenarioContext
        {
            public bool ExceptionReceived { get; set; }
        }

        public class ReceiverEndPoint : EndpointConfigurationBuilder
        {
            public ReceiverEndPoint()
            {
                EndpointSetup<DefaultServer>();
            }
        }

        [TimeToBeReceived("31.00:00:01")]
        public class MessageNotToBeSent : IMessage
        {
        }
    }
}