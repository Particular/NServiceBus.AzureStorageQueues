namespace NServiceBus.Transport.AzureStorageQueues.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Features;
    using global::Azure.Storage.Queues;
    using AcceptanceTesting.Customization;
    using Microsoft.Azure.Cosmos.Table;
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
                 .WithEndpoint<Subscriber>(b => b.When(async (session, c) =>
                 {
                     await session.Subscribe<MyEvent>();
                     c.Subscribed = true;
                 }))
                 .Done(c => c.WasCalled)
                 .Run().ConfigureAwait(false);

            Assert.IsTrue(context.WasCalled);
        }

        const string SubscriberAccount = "subscriber";
        const string PublisherAccount = "publisher";

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

                    transport.AccountRouting.DefaultAccountAlias = PublisherAccount;

                    var anotherAccount = transport.AccountRouting.AddAccount(SubscriberAccount,
                        new QueueServiceClient(Utilities.GetEnvConfiguredConnectionString2()),
                        CloudStorageAccount.Parse(Utilities.GetEnvConfiguredConnectionString2()).CreateCloudTableClient());
                    anotherAccount.AddEndpoint(Conventions.EndpointNamingConvention(typeof(Subscriber)));
                });
            }
        }

        public class Subscriber : EndpointConfigurationBuilder
        {
            public Subscriber()
            {
                var transport = Utilities.CreateTransportWithDefaultTestsConfiguration(Utilities.GetEnvConfiguredConnectionString2());

                transport.AccountRouting.DefaultAccountAlias = SubscriberAccount;

                var anotherAccount = transport.AccountRouting.AddAccount(PublisherAccount,
                    new QueueServiceClient(Utilities.GetEnvConfiguredConnectionString()),
                    CloudStorageAccount.Parse(Utilities.GetEnvConfiguredConnectionString()).CreateCloudTableClient());
                anotherAccount.AddEndpoint(Conventions.EndpointNamingConvention(typeof(Publisher)), new[] { typeof(MyEvent) });

                EndpointSetup(
                    endpointTemplate: new CustomizedServer(transport),
                    configurationBuilderCustomization: (config, rd) =>
                    {
                        config.DisableFeature<AutoSubscribe>();
                    });
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