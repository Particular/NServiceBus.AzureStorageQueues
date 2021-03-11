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

    public class When_dispatching_to_other_account_using_connection_string : NServiceBusAcceptanceTest
    {
        [Test]
        public void Should_fail()
        {
            var exception = Assert.ThrowsAsync<Exception>(async () =>
            {
                await Scenario.Define<Context>()
                    .WithEndpoint<Sender>(b =>
                    {
                        b.When((bus, c) =>
                        {
                            var options = new SendOptions();
                            options.SetDestination(
                                $"{Conventions.EndpointNamingConvention(typeof(Receiver))}@{Utilities.GetEnvConfiguredConnectionString()}");
                            return bus.Send(new MyMessage(), options);
                        });
                    })
                    .WithEndpoint<Receiver>()
                    .Done(c => c.WasCalled)
                    .Run().ConfigureAwait(false);
            });

            Assert.AreEqual("An attempt to use an address with a connection string using the 'destination@connectionstring' format was detected. Only aliases are allowed. Provide an alias for the storage account.", exception.Message);
        }

        const string Alias = "another";
        const string DefaultAccountName = "default";

        public class Context : ScenarioContext
        {
            public bool WasCalled { get; set; }
        }

        class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServer>(configuration =>
                {
                    var transport = configuration.ConfigureTransport<AzureStorageQueueTransport>();
                    transport.AccountRouting.DefaultAccountAlias = DefaultAccountName;
                    transport.AccountRouting.AddAccount(Alias, new QueueServiceClient(Utilities.GetEnvConfiguredConnectionString2()), CloudStorageAccount.Parse(Utilities.GetEnvConfiguredConnectionString2()).CreateCloudTableClient());

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

                EndpointSetup(new CustomizedServer(transport), (configuration, rd) =>
                {
                    transport.AccountRouting.DefaultAccountAlias = Alias;
                });
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