namespace NServiceBus.Transport.AzureStorageQueues.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Features;
    using global::Azure.Storage.Queues;
    using NServiceBus.AcceptanceTesting.Customization;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;
    using Testing;

    public class When_subscribing_to_another_account_with_registered_endpoint : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Account_mapped_should_be_respected()
        {
            var context = await Scenario.Define<Context>()
                 .WithEndpoint<Publisher>(b =>
                {
                    b.When(c => c.Subscribed, session => session.Publish<MyEvent>());
                })
                 .WithEndpoint<Subscriber>(b => b.When(async (session, c) => { await session.Subscribe<MyEvent>(); }))
                 .Done(c => c.WasCalled)
                 .Run().ConfigureAwait(false);

            Assert.IsTrue(context.WasCalled);
        }

        const string AnotherAccountName = "another";
        const string DefaultAccountName = "default";

        public class Context : ScenarioContext
        {
            public bool Subscribed { get; set; }
            public bool WasCalled { get; set; }
        }

        class Publisher : EndpointConfigurationBuilder
        {
            public Publisher()
            {
                EndpointSetup<DefaultPublisher>(configuration =>
                {
                    var transport = configuration.ConfigureTransport<AzureStorageQueueTransport>();

                    transport.AccountRouting.DefaultAccountAlias = DefaultAccountName;
                    var anotherAccount = transport.AccountRouting.AddAccount(AnotherAccountName, new QueueServiceClient(Utilities.GetEnvConfiguredConnectionString2()));
                    anotherAccount.RegisteredEndpoints.Add(Conventions.EndpointNamingConvention(typeof(Subscriber)));

                    configuration.OnEndpointSubscribed<Context>((s, context) => { context.Subscribed = true; });
                });
            }
        }

        public class Subscriber : EndpointConfigurationBuilder
        {
            public Subscriber()
            {
                var transport = Utilities.CreateTransportWithDefaultTestsConfiguration(Utilities.GetEnvConfiguredConnectionString2());

                transport.AccountRouting.DefaultAccountAlias = AnotherAccountName;
                var anotherAccount = transport.AccountRouting.AddAccount(DefaultAccountName, new QueueServiceClient(Utilities.GetEnvConfiguredConnectionString()));
                anotherAccount.RegisteredEndpoints.Add(Conventions.EndpointNamingConvention(typeof(Publisher)));

                EndpointSetup(
                    endpointTemplate: new CustomizedServer(transport),
                    configurationBuilderCustomization: (config, rd) =>
                    {
                        config.DisableFeature<AutoSubscribe>();
                    },
                    publisherMetadata: p => p.RegisterPublisherFor<MyEvent>(typeof(Publisher)));
            }

            public class MyMessageHandler : IHandleMessages<MyEvent>
            {
                Context testContext;

                public MyMessageHandler(Context testContext)
                {
                    this.testContext = testContext;
                }

                public Task Handle(MyEvent message, IMessageHandlerContext context)
                {
                    testContext.WasCalled = true;
                    return Task.FromResult(0);
                }
            }
        }

        [Serializable]
        public class MyEvent : IEvent
        {
        }
    }
}