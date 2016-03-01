namespace NServiceBus.Azure.Transports.WindowsAzureStorageQueues.AcceptanceTests.Sending
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_message_is_sent_with_time_to_be_received_set_to_more_than_7_days : NServiceBusAcceptanceTest
    {
        [Test]
        public async void Should_throw_exception()
        {
            await Scenario.Define<Context>()
                .WithEndpoint<ReceiverEndPoint>(b => b.When((bus, c) =>
                {
                    var exception = Assert.Throws<InvalidOperationException>(async () => await bus.SendLocal(new MessageNotToBeSent()));
                    var expectedMessage = $"TimeToBeReceived is set to more than 7 days (maximum for Azure Storage queue) for message type '{typeof(MessageNotToBeSent).FullName}'.";
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
            public Context Context { get; set; }

            public ReceiverEndPoint()
            {
                EndpointSetup<DefaultServer>();
            }
        }

        [TimeToBeReceived("7.00:00:01")]
        public class MessageNotToBeSent : IMessage
        {
        }
    }
}
