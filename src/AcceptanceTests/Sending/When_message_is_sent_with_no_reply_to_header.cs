namespace NServiceBus.Azure.Transports.WindowsAzureStorageQueues.AcceptanceTests.Sending
{
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NServiceBus.MessageMutator;
    using NServiceBus.Unicast.Messages;
    using NUnit.Framework;

    public class When_message_is_sent_with_no_reply_to_header : NServiceBusAcceptanceTest
    {
        [Test]
        public void Should_dispatch_properly()
        {
            var context = new Context();

            Scenario.Define(context)
                .WithEndpoint<ReceiverEndPoint>(b =>
                {
                    b.Given((bus, c) =>
                    {
                        bus.SendLocal(new Message());
                    });
                })
                .Run();


            Assert.IsTrue(context.WasCalled);
        }

        public class Context : ScenarioContext
        {
            public bool WasCalled { get; set; }
        }

        public class ReceiverEndPoint : EndpointConfigurationBuilder
        {
            public ReceiverEndPoint()
            {
                EndpointSetup<DefaultServer>();
            }

            public class MyMessageHandler : IHandleMessages<Message>
            {
                public Context Context { get; set; }

                public IBus Bus { get; set; }

                public void Handle(Message message)
                {
                    Context.WasCalled = true;
                }
            }

        }

        class ClearReplyToHeader : IMutateOutgoingTransportMessages
        {
            public void MutateOutgoing(LogicalMessage logicalMessage, TransportMessage transportMessage)
            {
                transportMessage.Headers.Remove("NServiceBus.ReplyToAddress");
            }
        }

        public class Message : IMessage
        {
        }
    }
}
