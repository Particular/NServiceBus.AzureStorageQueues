namespace NServiceBus.Transport.AzureStorageQueues.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using Features;
    using global::Azure.Data.Tables;
    using global::Azure.Storage.Queues;
    using NServiceBus.AcceptanceTesting.EndpointTemplates;
    using NUnit.Framework;
    using Testing;

    public class When_subscribing_to_another_account_with_registered_endpoint : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Account_mapped_should_be_respected()
        {
            var context = await Scenario.Define<Context>()
                 .WithEndpoint<Publisher>(b => b.When(c => c.Subscribed, session => session.Publish<MyEvent>()))
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
            public Publisher() => EndpointSetup<DefaultPublisher>(
                configuration =>
                {
                    var transport = configuration.ConfigureTransport<AzureStorageQueueTransport>();

                    transport.AccountRouting.DefaultAccountAlias = PublisherAccount;

                    var anotherAccount = transport.AccountRouting.AddAccount(SubscriberAccount,
                        new QueueServiceClient(Utilities.GetEnvConfiguredConnectionString2()),
                        new TableServiceClient(Utilities.GetEnvConfiguredConnectionString2()));
                    anotherAccount.AddEndpoint(Conventions.EndpointNamingConvention(typeof(Subscriber)));
                });
        }

        public class Subscriber : EndpointConfigurationBuilder
        {
            public Subscriber()
            {
                var transport = Utilities.CreateTransportWithDefaultTestsConfiguration(Utilities.GetEnvConfiguredConnectionString2());

                transport.AccountRouting.DefaultAccountAlias = SubscriberAccount;

                var anotherAccount = transport.AccountRouting.AddAccount(PublisherAccount,
                    new QueueServiceClient(Utilities.GetEnvConfiguredConnectionString()),
                    new TableServiceClient(Utilities.GetEnvConfiguredConnectionString()));
                anotherAccount.AddEndpoint(Conventions.EndpointNamingConvention(typeof(Publisher)), new[] { typeof(MyEvent) });

                EndpointSetup(
                    endpointTemplate: new CustomizedServer(transport),
                    configurationBuilderCustomization: (config, rd) => config.DisableFeature<AutoSubscribe>());
            }

            public class MyMessageHandler : IHandleMessages<MyEvent>
            {
                readonly Context testContext;

                public MyMessageHandler(Context testContext) => this.testContext = testContext;

                public Task Handle(MyEvent message, IMessageHandlerContext context)
                {
                    testContext.WasCalled = true;
                    return Task.CompletedTask;
                }
            }
        }

        [Serializable]
        public class MyEvent : IEvent
        {
        }
    }
}