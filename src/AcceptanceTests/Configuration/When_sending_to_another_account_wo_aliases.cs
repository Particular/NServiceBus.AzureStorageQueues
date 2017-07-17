namespace NServiceBus.AcceptanceTests.WindowsAzureStorageQueues.Configuration
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using Azure.Transports.WindowsAzureStorageQueues.AcceptanceTests;
    using EndpointTemplates;
    using NUnit.Framework;

    public class When_sending_to_another_account_wo_aliases : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_properly_handle_it()
        {
            var queue = Conventions.EndpointNamingConvention(typeof(Receiver));
            var connectionString = Utils.GetEnvConfiguredConnectionString();

            var another = Utils.BuildAnotherConnectionString(connectionString);
            var queueAddress = queue + "@" + another;
            
            var context = await Scenario.Define<Context>()
                .WithEndpoint<Receiver>(c => c.When(s =>
                {
                    return s.Send<MyMessage>(queueAddress, m => { });
                }))
                .Done(c => c.HandlerCalled)
                .Run(TimeSpan.FromSeconds(15)).ConfigureAwait(false);

            Assert.True(context.HandlerCalled);
        }

        public class Context : ScenarioContext
        {
            public bool HandlerCalled { get; set; }
        }

        public class Receiver : EndpointConfigurationBuilder
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
                scenarioContext.HandlerCalled = true;
                return Task.FromResult(0);
            }
        }
    }
}