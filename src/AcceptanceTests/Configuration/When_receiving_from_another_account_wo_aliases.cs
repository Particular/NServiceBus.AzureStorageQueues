namespace NServiceBus.AcceptanceTests.WindowsAzureStorageQueues.Configuration
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using EndpointTemplates;
    using NUnit.Framework;

    public class When_receiving_from_another_account_wo_aliases : NServiceBusAcceptanceTest
    {
        const string UnmappedConnectionString = "queue@DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;";

        [Test]
        public async Task Should_properly_handle_it()
        {
            var context = await Scenario.Define<Context>()
                .WithEndpoint<Receiver>(c => c.When(s =>
                {
                    var options = new SendOptions();
                    options.RouteToThisEndpoint();
                    options.RouteReplyTo(UnmappedConnectionString);
                    return s.Send(new MyMessage(), options);
                }))
                .Done(c => c.HandlerCalled)
                .Run(TimeSpan.FromSeconds(15)).ConfigureAwait(false);

            Assert.True(context.HandlerCalled);
        }

        public class Context : ScenarioContext
        {
            public bool HandlerCalled { get; set; }
        }

        class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServer>();
            }
        }

        public class MyMessage : IMessage
        {
        }

        public class Handler : IHandleMessages<MyMessage>
        {
            readonly Context scenarioContext;

            public Handler(Context scenarioContext)
            {
                this.scenarioContext = scenarioContext;
            }

            public Task Handle(MyMessage message, IMessageHandlerContext context)
            {
                if (context.MessageHeaders[Headers.ReplyToAddress] == UnmappedConnectionString)
                {
                    scenarioContext.HandlerCalled = true;
                }
                return Task.FromResult(0);
            }
        }
    }
}