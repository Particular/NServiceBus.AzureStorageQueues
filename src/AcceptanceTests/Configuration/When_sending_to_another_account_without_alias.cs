namespace NServiceBus.AcceptanceTests.WindowsAzureStorageQueues.Configuration
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using EndpointTemplates;
    using NUnit.Framework;

    public class When_sending_to_another_account_without_alias : NServiceBusAcceptanceTest
    {
        [Test]
        public Task Should_throw()
        {
            var queue = Conventions.EndpointNamingConvention(typeof(Receiver));

            var another = ConfigureEndpointAzureStorageQueueTransport.AnotherConnectionString;
            var queueAddress = queue + "@" + another;

            var exception = Assert.ThrowsAsync<Exception>(async () =>
            {
                await Scenario.Define<Context>()
                    .WithEndpoint<Receiver>(c => c.When(s =>
                    {
                        return s.Send<MyMessage>(queueAddress, m => { });
                    }))
                    .Run(TimeSpan.FromSeconds(15)).ConfigureAwait(false);
            });

            Assert.AreEqual($"No account was mapped under following name '{another}'. Please map it using .AccountRouting().AddAccount() method.", exception.Message);

            return Task.CompletedTask;
        }

        public class Context : ScenarioContext
        {
            public bool HandlerCalled { get; set; }
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServer>(configuration =>
                {
                    configuration.UseTransport<AzureStorageQueueTransport>()
                        .ConnectionString(ConfigureEndpointAzureStorageQueueTransport.AnotherConnectionString);
                });
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