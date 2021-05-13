namespace NServiceBus.Transport.AzureStorageQueues.AcceptanceTests
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using global::Azure.Storage.Queues;
    using Microsoft.Azure.Cosmos.Table;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;
    using Testing;

    public class When_replying_to_a_message_sent_using_alias : NServiceBusAcceptanceTest
    {
        [Test]
        [Ignore("Test does not actually work when using independent storage accounts")]
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
                    var receiverAccountInfo = configuration.UseTransport<AzureStorageQueueTransport>()
                        .DefaultAccountAlias(SenderAlias)
                        .ConnectionString(Utilities.GetEnvConfiguredConnectionString)
                        .AccountRouting()
                        .AddAccount(ReceiverAlias, new QueueServiceClient(Utilities.GetEnvConfiguredConnectionString2()), CloudStorageAccount.Parse(Utilities.GetEnvConfiguredConnectionString2()).CreateCloudTableClient());

                    // Route MyMessage messages to the receiver endpoint configured to use receiver alias (on a different storage account)
                    var receiverEndpointName = Conventions.EndpointNamingConvention(typeof(Receiver));
                    receiverAccountInfo.AddEndpoint(receiverEndpointName);
                    configuration.ConfigureTransport().Routing().RouteToEndpoint(typeof(MyMessage), receiverEndpointName);
                });
            }

            public class MyReplyMessageHandler : IHandleMessages<MyReplyMessage>
            {
                readonly Context testContext;

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
                EndpointSetup<DefaultServer>(configuration =>
                {
                    var senderEndpointAccountInfo = configuration.UseTransport<AzureStorageQueueTransport>()
                        .DefaultAccountAlias(ReceiverAlias)
                        .ConnectionString(ConfigureEndpointAzureStorageQueueTransport.AnotherConnectionString)
                        .AccountRouting()
                        .AddAccount(SenderAlias, new QueueServiceClient(Utilities.GetEnvConfiguredConnectionString()), CloudStorageAccount.Parse(Utilities.GetEnvConfiguredConnectionString()).CreateCloudTableClient());

                    // Route MyMessage messages to the receiver endpoint configured to use sender alias (on a different storage account)
                    var senderEndpointName = Conventions.EndpointNamingConvention(typeof(Sender));
                    senderEndpointAccountInfo.AddEndpoint(senderEndpointName);
                    // TODO: remove if not necessary
                    //configuration.ConfigureTransport().Routing().RouteToEndpoint(typeof(MyReplyMessage), senderEndpointName);
                });
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                readonly Context testContext;

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