namespace NServiceBus.Transport.AzureStorageQueues.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;
    using NServiceBus.Pipeline;
    using Testing;

    public class When_replying_to_a_message_with_reply_to_header_containing_connection_string : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_work()
        {
            var context = await Scenario.Define<Context>()
                .WithEndpoint<Sender>(b =>
                {
                    b.When((bus, c) =>
                    {
                        var options = new SendOptions();
                        options.SetDestination($"{Conventions.EndpointNamingConvention(typeof(Receiver))}@{ReceiverAlias}");
                        return bus.Send(new MyMessage(), options);
                    });
                })
                .WithEndpoint<Receiver>()
                .Done(c => c.ReplyReceived)
                .Run().ConfigureAwait(false);

            Assert.IsTrue(context.ReplyReceived);
        }

        const string SenderAlias = "sender";
        const string ReceiverAlias = "receiver";

        public class Context : ScenarioContext
        {
            public bool ReplyReceived { get; set; }
        }

        class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServer>(configuration =>
                {
                    var transport = configuration.ConfigureTransport<AzureStorageQueueTransport>();

#pragma warning disable IDE0079
#pragma warning disable CS0618

                    transport.AccountRouting.DefaultAccountAlias = SenderAlias;
                    var receiverAccountInfo = transport.AccountRouting.AddAccount(ReceiverAlias, Utilities.GetEnvConfiguredConnectionString2());

#pragma warning restore CS0618
#pragma warning restore IDE0079

                    // Route MyMessage messages to the receiver endpoint configured to use receiver alias (on a different storage account)
                    var receiverEndpointName = Conventions.EndpointNamingConvention(typeof(Receiver));
                    receiverAccountInfo.RegisteredEndpoints.Add(receiverEndpointName);

                    var routing = configuration.ConfigureRouting();
                    routing.RouteToEndpoint(typeof(MyMessage), receiverEndpointName);

                    configuration.Pipeline.Register(typeof(VerifyReplyMessage), "Verifies the expected reply message has arrived.");
                });
            }

            class VerifyReplyMessage : Behavior<IIncomingPhysicalMessageContext>
            {
                Context testContext;

                public VerifyReplyMessage(Context testContext)
                {
                    this.testContext = testContext;
                }

                public override Task Invoke(IIncomingPhysicalMessageContext context, Func<Task> next)
                {
                    if (context.Message.Headers.TryGetValue("reply-message-as-expected", out _))
                    {
                        testContext.ReplyReceived = true;
                    }

                    return Task.CompletedTask;
                }
            }
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                var transport = Utilities.CreateTransportWithDefaultTestsConfiguration(Utilities.GetEnvConfiguredConnectionString2());

                EndpointSetup(new CustomizedServer(transport), (configuration, rd) =>
                {
#pragma warning disable IDE0079
#pragma warning disable CS0618

                    transport.AccountRouting.DefaultAccountAlias = ReceiverAlias;

#pragma warning restore CS0618
#pragma warning restore IDE0079

                    configuration.Pipeline.Register(typeof(OverrideReplyToHeaderWithConnectionString), "Override reply-to header with connection string to emulate an older endpoint.");
                });
            }

            class OverrideReplyToHeaderWithConnectionString : Behavior<IIncomingPhysicalMessageContext>
            {
                public override Task Invoke(IIncomingPhysicalMessageContext context, Func<Task> next)
                {
                    var replyOptions = new ReplyOptions();
                    replyOptions.SetDestination(context.Message.Headers[Headers.ReplyToAddress].Replace(SenderAlias, Utilities.GetEnvConfiguredConnectionString()));
                    replyOptions.SetHeader("reply-message-as-expected", "OK");

                    return context.Reply(new MyMessageReply(), replyOptions);
                }
            }
        }

        public class MyMessage : ICommand
        {
        }

        public class MyMessageReply : IMessage
        {
        }
    }
}