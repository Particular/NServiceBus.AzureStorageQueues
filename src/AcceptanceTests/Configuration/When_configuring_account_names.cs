namespace NServiceBus.AcceptanceTests.WindowsAzureStorageQueues.Configuration
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using EndpointTemplates;
    using NUnit.Framework;

    public class When_configuring_account_names : NServiceBusAcceptanceTest
    {
        public When_configuring_account_names()
        {
            connectionString = Testing.Utilities.GetEnvConfiguredConnectionString();
            anotherConnectionString = Testing.Utilities.GetEnvConfiguredConnectionString2();
        }

        [Test]
        public Task Should_accept_no_mappings_at_all()
        {
            return Configure(_ => { });
        }

        [Test]
        public void Should_not_accept_mappings_without_default()
        {
            var exception = Assert.CatchAsync(() => { return Configure(cfg => { cfg.AccountRouting().AddAccount(Another, anotherConnectionString); }); });
            Assert.IsTrue(exception.Message.Contains("The mapping of account names instead of connection strings is enabled but the default connection string name isn\'t provided. Provide the default connection string name when adding more accounts"), "Message is missing or incorrect");
        }

        [Test]
        public Task Should_accept_mappings_with_default()
        {
            return Configure(cfg =>
            {
                cfg.DefaultAccountAlias(Default);
                cfg.AccountRouting().AddAccount(Another, anotherConnectionString);
            });
        }

        Task Configure(Action<TransportExtensions<AzureStorageQueueTransport>> action)
        {
            return Scenario.Define<Context>()
                .WithEndpoint<Endpoint>(cfg =>
                {
                    cfg.CustomConfig(c =>
                    {
                        c.UseSerialization<NewtonsoftSerializer>();
                        var transport = c.UseTransport<AzureStorageQueueTransport>();
                        transport
                            .ConnectionString(connectionString)
                            .SerializeMessageWrapperWith<TestIndependence.TestIdAppendingSerializationDefinition<NewtonsoftSerializer>>();

                        action(transport);
                    });

                    cfg.When((bus, c) =>
                    {
                        var options = new SendOptions();
                        options.SetDestination("ConfiguringAccountNames.Receiver");
                        return bus.Send(new MyMessage(), options);
                    });
                })
                .WithEndpoint<Receiver>()
                .Done(c => c.WasCalled)
                .Run();
        }

        readonly string connectionString;
        readonly string anotherConnectionString;
        const string Default = "default";
        const string Another = "another";

        class Context : ScenarioContext
        {
            public bool WasCalled { get; set; }
        }

        class Endpoint : EndpointConfigurationBuilder
        {
            public Endpoint()
            {
                EndpointSetup<DefaultServer>(endpointConfiguration =>
                {
                    endpointConfiguration.SendOnly();
                });
            }
        }

        class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.UseSerialization<NewtonsoftSerializer>();
                    c.UseTransport<AzureStorageQueueTransport>();
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