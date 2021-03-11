namespace NServiceBus.Transport.AzureStorageQueues.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using global::Azure.Storage.Queues;
    using Microsoft.Azure.Cosmos.Table;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;
    using Testing;

    public class When_dispatching_to_another_account_with_registered_endpoint : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Account_mapped_should_be_respected()
        {
            var context = await Scenario.Define<Context>()
                 .WithEndpoint<Endpoint>(b =>
                 {
                     b.When((bus, c) => bus.Send(new MyMessage()));
                 })
                 .WithEndpoint<Receiver>()
                 .Done(c => c.WasCalled)
                 .Run().ConfigureAwait(false);

            Assert.IsTrue(context.WasCalled);
        }

        const string AnotherAccountName = "another";
        const string DefaultAccountName = "default";

        public class Context : ScenarioContext
        {
            public string SendTo { get; set; }
            public bool WasCalled { get; set; }
        }

        class Endpoint : EndpointConfigurationBuilder
        {
            public Endpoint()
            {
                EndpointSetup<DefaultServer>(configuration =>
                {
                    var transport = configuration.ConfigureTransport<AzureStorageQueueTransport>();
                    transport.AccountRouting.DefaultAccountAlias = DefaultAccountName;

                    var anotherAccount = transport.AccountRouting.AddAccount(AnotherAccountName, new QueueServiceClient(Utilities.GetEnvConfiguredConnectionString2()), CloudStorageAccount.Parse(Utilities.GetEnvConfiguredConnectionString2()).CreateCloudTableClient());
                    anotherAccount.AddEndpoint(Conventions.EndpointNamingConvention(typeof(Receiver)));

                    var routing = configuration.ConfigureRouting();
                    routing.RouteToEndpoint(typeof(MyMessage), typeof(Receiver));
                });
            }
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                var transport = Utilities.CreateTransportWithDefaultTestsConfiguration(Utilities.GetEnvConfiguredConnectionString2());

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
                    testContext.WasCalled = true;
                    return Task.FromResult(0);
                }
            }
        }

        [Serializable]
        public class MyMessage : ICommand
        {
        }
    }
}