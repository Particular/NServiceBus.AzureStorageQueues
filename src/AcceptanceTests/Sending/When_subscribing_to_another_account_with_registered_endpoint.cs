namespace NServiceBus.Transport.AzureStorageQueues.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using Features;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

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
                EndpointSetup<DefaultServer>(configuration =>
                {
                    var routing = configuration.UseTransport<AzureStorageQueueTransport>()
                        .DefaultAccountAlias(PublisherAccount)
                        .ConnectionString(ConfigureEndpointAzureStorageQueueTransport.ConnectionString)
                        .AccountRouting();

                    var anotherAccount = routing.AddAccount(SubscriberAccount, ConfigureEndpointAzureStorageQueueTransport.AnotherConnectionString);
                    anotherAccount.AddEndpoint(Conventions.EndpointNamingConvention(typeof(Subscriber)));
                });

            }
        }

        public class Subscriber : EndpointConfigurationBuilder
        {
            public Subscriber()
            {
                EndpointSetup<DefaultServer>(configuration =>
                {
                    configuration.DisableFeature<AutoSubscribe>();

                    var transportConfig = configuration.UseTransport<AzureStorageQueueTransport>();
                    var accountRouting = transportConfig
                        .DefaultAccountAlias(SubscriberAccount)
                        .ConnectionString(ConfigureEndpointAzureStorageQueueTransport.AnotherConnectionString)
                        .AccountRouting();

                    var anotherAccount = accountRouting.AddAccount(PublisherAccount, ConfigureEndpointAzureStorageQueueTransport.ConnectionString);
                    anotherAccount.AddEndpoint(Conventions.EndpointNamingConvention(typeof(Publisher)), new[] { typeof(MyEvent) });
                });
            }

            public class MyMessageHandler : IHandleMessages<MyEvent>
            {
                public Context Context { get; set; }

                public Task Handle(MyEvent message, IMessageHandlerContext context)
                {
                    Context.WasCalled = true;
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