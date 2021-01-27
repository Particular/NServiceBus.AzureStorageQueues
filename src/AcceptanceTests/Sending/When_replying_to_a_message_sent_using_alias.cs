#pragma warning disable CS0618 // Type or member is obsolete

namespace NServiceBus.Transport.AzureStorageQueues.AcceptanceTests
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;
    using Testing;

    public class When_replying_to_a_message_sent_using_alias : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_work()
        {
            var context = await Scenario.Define<Context>()
                .WithEndpoint<Sender>(b =>
                {
                    b.When((bus, c) => bus.Send(new MyMessage()));
                })
                .WithEndpoint<Receiver>()
                .Done(c => c.IsDone)
                .Run().ConfigureAwait(false);

            Assert.IsTrue(context.ReceiverWasCalled);
            Assert.IsTrue(context.ReplyMessageReceived);
        }

        const string SenderAlias = "sender";
        const string ReceiverAlias = "receiver";

        public class Context : ScenarioContext
        {
            public bool ReceiverWasCalled { get; set; }
            public bool ReplyMessageReceived { get; set; }

            public bool IsDone => ReceiverWasCalled && ReplyMessageReceived;
        }

        class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServer>(configuration =>
                {
                    var transport = new AzureStorageQueueTransport(Utilities.GetEnvConfiguredConnectionString());
                    transport.AccountRouting.DefaultAccountAlias = SenderAlias;
                    var receiverAccountInfo = transport.AccountRouting.AddAccount(ReceiverAlias, Utilities.GetEnvConfiguredConnectionString2());

                    // Route MyMessage messages to the receiver endpoint configured to use receiver alias (on a different storage account)
                    var receiverEndpointName = Conventions.EndpointNamingConvention(typeof(Receiver));
                    receiverAccountInfo.RegisteredEndpoints.Add(receiverEndpointName);

                    var routing = configuration.UseTransport(transport);

                    routing.RouteToEndpoint(typeof(MyMessage), receiverEndpointName);
                });
            }

            public class MyReplyMessageHandler : IHandleMessages<MyReplyMessage>
            {
                readonly Context _testContext;

                public MyReplyMessageHandler(Context testContext)
                {
                    _testContext = testContext;
                }

                public Task Handle(MyReplyMessage message, IMessageHandlerContext context)
                {
                    _testContext.ReplyMessageReceived = true;
                    return Task.CompletedTask;
                }
            }
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServer>(configuration =>
                {
                    var transport = new AzureStorageQueueTransport(Utilities.GetEnvConfiguredConnectionString2());
                    transport.AccountRouting.DefaultAccountAlias = ReceiverAlias;
                    var senderEndpointAccountInfo = transport.AccountRouting.AddAccount(SenderAlias, Utilities.GetEnvConfiguredConnectionString());

                    // Route MyMessage messages to the receiver endpoint configured to use sender alias (on a different storage account)
                    var senderEndpointName = Conventions.EndpointNamingConvention(typeof(Sender));
                    senderEndpointAccountInfo.RegisteredEndpoints.Add(senderEndpointName);

                    configuration.UseTransport(transport);
                });
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                readonly Context _testContext;

                public MyMessageHandler(Context testContext)
                {
                    _testContext = testContext;
                }

                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    _testContext.ReceiverWasCalled = true;
                    return context.Reply(new MyReplyMessage());
                }
            }
        }

        public class MyMessage : ICommand
        {
        }

        public class MyReplyMessage : IMessage
        {
        }

    }
}

#pragma warning restore CS0618 // Type or member is obsolete