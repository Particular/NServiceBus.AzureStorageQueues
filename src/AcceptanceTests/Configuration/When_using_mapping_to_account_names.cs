namespace NServiceBus.AcceptanceTests.WindowsAzureStorageQueues.Configuration
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using EndpointTemplates;
    using NUnit.Framework;

    public class When_using_mapping_to_account_names : NServiceBusAcceptanceTest
    {
        const string SenderName = "mapping-names-sender";
        const string ReceiverName = "mapping-names-receiver";
        const string AuditName = "mapping-names-audit";

        [Test]
        public Task Should_send_just_with_queue_name_for_same_account_without_names_mapping()
        {
            return SendMessage<SenderUsingConnectionStrings>();
        }

        [Test]
        public Task Should_send_just_with_queue_name_for_same_account_with_names_mapping()
        {
            return SendMessage<SenderUsingNamesInsteadConnectionStrings>();
        }

        static async Task SendMessage<TEndpointConfigurationBuilder>() 
            where TEndpointConfigurationBuilder : EndpointConfigurationBuilder
        {
            var ctx = await Scenario.Define<Context>()
                            .WithEndpoint<TEndpointConfigurationBuilder>(b => b.When(s =>
                            {
                                var options = new SendOptions();
                                options.RouteReplyTo(SenderName);
                                options.SetDestination(ReceiverName);
                                return s.Send(new MyCommand(), options);
                            }))
                            .WithEndpoint<Receiver>()
                            .Done(c => c.Received)
                            .Run();

            Assert.IsTrue(ctx.Received);
        }

        class Context : ScenarioContext
        {
            public bool Received { get; set; }
        }

        class SenderUsingNamesInsteadConnectionStrings : EndpointConfigurationBuilder
        {
            public SenderUsingNamesInsteadConnectionStrings()
            {
                EndpointSetup<DefaultServer>(cfg =>
                {
                    cfg.UseTransport<AzureStorageQueueTransport>()
                        .Addressing().UseAccountNamesInsteadOfConnectionStrings();
                });
            }
        }

        class SenderUsingConnectionStrings : EndpointConfigurationBuilder
        {
            public SenderUsingConnectionStrings()
            {
                EndpointSetup<DefaultServer>(cfg =>
                {
                    cfg.UseTransport<AzureStorageQueueTransport>();
                });
            }
        }

        class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultPublisher>();
                CustomEndpointName(ReceiverName);
                AuditTo(AuditName);
            }

            public class Handler : IHandleMessages<MyCommand>
            {
                public Context Context { get; set; }

                public Task Handle(MyCommand message, IMessageHandlerContext context)
                {
                    Context.Received = true;
                    return Task.FromResult(0);
                }
            }
        }

        class MyCommand : ICommand
        {
        }
    }
}