#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable CS0618 // Type or member is obsolete

namespace NServiceBus.Transport.AzureStorageQueues.AcceptanceTests
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
            _connectionString = Testing.Utilities.GetEnvConfiguredConnectionString();
            _anotherConnectionString = Testing.Utilities.GetEnvConfiguredConnectionString2();
        }

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
                    transport.AccountRouting.AddAccount(Another, _anotherConnectionString);
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
                transport.AccountRouting.AddAccount(Another, _anotherConnectionString);
            });
        }

        Task Configure(Action<AzureStorageQueueTransport> configureTransport)
        {
            return Scenario.Define<Context>()
                .WithEndpoint<SendOnlyEndpoint>(cfg =>
                {
                    cfg.CustomConfig(c =>
                    {
                        c.UseSerialization<NewtonsoftSerializer>();
                        var transport = new AzureStorageQueueTransport(_connectionString)
                        {
                            MessageWrapperSerializationDefinition = new TestIndependence.TestIdAppendingSerializationDefinition<NewtonsoftSerializer>(),
                            QueueNameSanitizer = BackwardsCompatibleQueueNameSanitizerForTests.Sanitize
                        };
                        transport.DelayedDelivery.DelayedDeliveryPoisonQueue = c.GetEndpointDefinedErrorQueue();

                        configureTransport(transport);
                        c.UseTransport(transport);
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

        readonly string _connectionString;
        readonly string _anotherConnectionString;
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
                    c.UseTransport(new AzureStorageQueueTransport(Testing.Utilities.GetEnvConfiguredConnectionString())
                    {
                        QueueNameSanitizer = BackwardsCompatibleQueueNameSanitizerForTests.Sanitize
                    });
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

#pragma warning restore CS0618 // Type or member is obsolete
#pragma warning restore IDE0079 // Remove unnecessary suppression