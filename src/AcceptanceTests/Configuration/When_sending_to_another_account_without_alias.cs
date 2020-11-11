namespace NServiceBus.Transport.AzureStorageQueues.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_sending_to_another_account_without_alias : NServiceBusAcceptanceTest
    {
        [Test]
        public void Should_throw()
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

            Assert.AreEqual("An attempt to use an address with a connection string using the 'destination@connectionstring' format was detected. Only aliases are allowed. Provide an alias for the storage account.", exception.Message);
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