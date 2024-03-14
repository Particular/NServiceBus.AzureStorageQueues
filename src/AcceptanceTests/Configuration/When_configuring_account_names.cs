﻿namespace NServiceBus.Transport.AzureStorageQueues.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
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
            Assert.IsTrue(exception.Message.Contains("The mapping of storage accounts connection strings to aliases is enforced but the the alias for the default connection string isn't provided"), "Exception message is missing or incorrect");
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
                        c.UseSerialization<NewtonsoftJsonSerializer>();
                        var transport = c.UseTransport<AzureStorageQueueTransport>();
                        transport
                            .ConnectionString(connectionString)
                            .SerializeMessageWrapperWith<TestIndependence.TestIdAppendingSerializationDefinition<NewtonsoftJsonSerializer>>();

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
                    c.UseSerialization<NewtonsoftJsonSerializer>();
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
        public class MyMessage : ICommand
        {
        }
    }
}