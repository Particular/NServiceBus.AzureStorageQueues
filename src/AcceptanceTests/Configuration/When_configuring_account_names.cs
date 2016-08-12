namespace NServiceBus.AcceptanceTests.WindowsAzureStorageQueues.Configuration
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Support;
    using Azure.Transports.WindowsAzureStorageQueues.AcceptanceTests;
    using EndpointTemplates;
    using NUnit.Framework;
    using Serializer = JsonSerializer;

    public class When_configuring_account_names : NServiceBusAcceptanceTest
    {
        public When_configuring_account_names()
        {
            connectionString = Utils.GetEnvConfiguredConnectionString();
            anotherConnectionString = Utils.BuildAnotherConnectionString(connectionString);
        }

        [Test]
        public Task Should_accept_no_mappings_at_all()
        {
            return Configure(_ => { });
        }

        [Test]
        public void Should_not_accept_mappings_without_default()
        {
            var ex = Assert.ThrowsAsync<AggregateException>(() => { return Configure(cfg => { cfg.AccountRouting().AddAccount(Another, anotherConnectionString); }); });
            var inner = ex.InnerExceptions.OfType<ScenarioException>().Single();
            Assert.IsInstanceOf<Exception>(inner.InnerException);
        }

        [Test]
        public Task Should_accept_mappings_with_default()
        {
            return Configure(cfg =>
            {
                cfg.DefaultAccountName(Default);
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
                        c.UseSerialization<Serializer>();
                        var transport = c.UseTransport<AzureStorageQueueTransport>();
                        transport
                            .UseAccountNamesInsteadOfConnectionStrings()
                            .ConnectionString(connectionString)
                            .SerializeMessageWrapperWith<JsonSerializer>();

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
                EndpointSetup<DefaultServer>().SendOnly();
            }
        }

        class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.UseSerialization<Serializer>();
                    c.UseTransport<AzureStorageQueueTransport>();
                });
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public Context Context { get; set; }

                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    Context.WasCalled = true;
                    return Task.FromResult(0);
                }
            }
        }

        [Serializable]
        class MyMessage : ICommand
        {
        }
    }
}