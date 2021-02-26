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
                    var transport = configuration.ConfigureTransport<AzureStorageQueueTransport>();
                    transport.AccountRouting.DefaultAccountAlias = SenderAlias;

#pragma warning disable IDE0079
#pragma warning disable CS0618

                    var receiverAccountInfo = transport.AccountRouting.AddAccount(ReceiverAlias, Utilities.GetEnvConfiguredConnectionString2());

#pragma warning restore CS0618
#pragma warning restore IDE0079

                    // Route MyMessage messages to the receiver endpoint configured to use receiver alias (on a different storage account)
                    var receiverEndpointName = Conventions.EndpointNamingConvention(typeof(Receiver));
                    receiverAccountInfo.RegisteredEndpoints.Add(receiverEndpointName);

                    configuration.ConfigureRouting()
                        .RouteToEndpoint(typeof(MyMessage), receiverEndpointName);
                });
            }

            public class MyReplyMessageHandler : IHandleMessages<MyReplyMessage>
            {
                Context testContext;

                public MyReplyMessageHandler(Context testContext)
                {
                    this.testContext = testContext;
                }

                public Task Handle(MyReplyMessage message, IMessageHandlerContext context)
                {
                    testContext.ReplyMessageReceived = true;
                    return Task.CompletedTask;
                }
            }
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                var transport = Utilities.CreateTransportWithDefaultTestsConfiguration(Utilities.GetEnvConfiguredConnectionString2());
                transport.AccountRouting.DefaultAccountAlias = ReceiverAlias;

#pragma warning disable IDE0079
#pragma warning disable CS0618

                var senderEndpointAccountInfo = transport.AccountRouting.AddAccount(SenderAlias, Utilities.GetEnvConfiguredConnectionString());

#pragma warning restore CS0618
#pragma warning restore IDE0079
                // Route MyMessage messages to the receiver endpoint configured to use sender alias (on a different storage account)
                var senderEndpointName = Conventions.EndpointNamingConvention(typeof(Sender));
                senderEndpointAccountInfo.RegisteredEndpoints.Add(senderEndpointName);

                EndpointSetup(new CustomizedServer(transport), (cfg, rd) => { });
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                Context testContext;

                public MyMessageHandler(Context testContext)
                {
                    this.testContext = testContext;
                }

                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    testContext.ReceiverWasCalled = true;
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