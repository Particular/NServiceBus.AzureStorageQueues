namespace NServiceBus.Transport.AzureStorageQueues.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using NServiceBus.AcceptanceTests;
    using NUnit.Framework;
    using Testing;

    public class When_sending_to_another_account_without_alias : NServiceBusAcceptanceTest
    {
        [Test]
        public void Should_throw()
        {
            var queue = Conventions.EndpointNamingConvention(typeof(Receiver));

            var another = Utilities.GetEnvConfiguredConnectionString2();
            var queueAddress = queue + "@" + another;

            var exception = Assert.ThrowsAsync<FormatException>(
                async () => await Scenario.Define<Context>()
                    .WithEndpoint<Receiver>(c => c.When(s => s.Send<MyMessage>(queueAddress, m => { })))
                    .Run(TimeSpan.FromSeconds(15)));

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
                var transport = Utilities.CreateTransportWithDefaultTestsConfiguration(Utilities.GetEnvConfiguredConnectionString2());
                EndpointSetup(new CustomizedServer(transport), (cfg, rd) => { });
            }
        }

        public class MyMessage : IMessage
        {
        }

        public class Handler : IHandleMessages<MyMessage>
        {
            readonly Context scenarioContext;

            public Handler(Context scenarioContext) => this.scenarioContext = scenarioContext;

            public Task Handle(MyMessage message, IMessageHandlerContext context)
            {
                scenarioContext.HandlerCalled = true;
                return Task.CompletedTask;
            }
        }
    }
}