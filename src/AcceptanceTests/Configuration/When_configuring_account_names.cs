﻿namespace NServiceBus.Transport.AzureStorageQueues.AcceptanceTests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using global::Azure.Storage.Queues;
    using Microsoft.Azure.Cosmos.Table;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;
    using Testing;

    public class When_configuring_account_names : NServiceBusAcceptanceTest
    {
        [Test]
        public Task Should_accept_no_mappings_at_all()
        {
            return Configure(_ => { });
        }

        [Test]
        public void Should_not_accept_mappings_without_default()
        {
            var exception = Assert.CatchAsync(() =>
            {
                return Configure(transport =>
                {
                    transport.AccountRouting.AddAccount(Another, new QueueServiceClient(Utilities.GetEnvConfiguredConnectionString2()), CloudStorageAccount.Parse(Utilities.GetEnvConfiguredConnectionString2()).CreateCloudTableClient());
                });
            });
            Assert.IsTrue(exception.Message.Contains("The mapping of storage accounts connection strings to aliases is enforced but the the alias for the default connection string isn't provided"), "Exception message is missing or incorrect");
        }

        [Test]
        public Task Should_accept_mappings_with_default()
        {
            return Configure(transport =>
            {
                transport.AccountRouting.DefaultAccountAlias = Default;
                transport.AccountRouting.AddAccount(Another, new QueueServiceClient(Utilities.GetEnvConfiguredConnectionString2()), CloudStorageAccount.Parse(Utilities.GetEnvConfiguredConnectionString2()).CreateCloudTableClient());
            });
        }

        Task Configure(Action<AzureStorageQueueTransport> customizeTransport, CancellationToken cancellationToken = default)
        {
            return Scenario.Define<Context>()
                .WithEndpoint<SendOnlyEndpoint>(cfg =>
                {
                    cfg.CustomConfig(c =>
                    {
                        c.UseSerialization<NewtonsoftSerializer>();

                        var transport = c.ConfigureTransport<AzureStorageQueueTransport>();
                        customizeTransport(transport);
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

        const string Default = "default";
        const string Another = "another";

        class Context : ScenarioContext
        {
            public bool WasCalled { get; set; }
        }

        class SendOnlyEndpoint : EndpointConfigurationBuilder
        {
            public SendOnlyEndpoint()
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