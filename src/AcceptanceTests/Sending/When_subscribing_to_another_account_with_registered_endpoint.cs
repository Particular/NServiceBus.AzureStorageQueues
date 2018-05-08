namespace NServiceBus.AcceptanceTests.WindowsAzureStorageQueues.Sending
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using EndpointTemplates;
    using Features;
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
                    var routing = configuration.UseTransport<AzureStorageQueueTransport>()
                        .UseAccountAliasesInsteadOfConnectionStrings()
                        .DefaultAccountAlias(DefaultAccountName)
                        .ConnectionString(ConfigureEndpointAzureStorageQueueTransport.ConnectionString)
                        .AccountRouting();

                    var anotherAccount = routing.AddAccount(AnotherAccountName, ConfigureEndpointAzureStorageQueueTransport.AnotherConnectionString);
                    anotherAccount.RegisteredEndpoints.Add(Conventions.EndpointNamingConvention(typeof(Subscriber)));

                    configuration.OnEndpointSubscribed<Context>((s, context) => { context.Subscribed = true; });
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
                        .UseAccountAliasesInsteadOfConnectionStrings()
                        .DefaultAccountAlias(AnotherAccountName)
                        .ConnectionString(ConfigureEndpointAzureStorageQueueTransport.AnotherConnectionString)
                        .AccountRouting();

                    var anotherAccount = accountRouting.AddAccount(DefaultAccountName, ConfigureEndpointAzureStorageQueueTransport.ConnectionString);
                    anotherAccount.RegisteredEndpoints.Add(Conventions.EndpointNamingConvention(typeof(Publisher)));

                    transportConfig.Routing().RegisterPublisher(typeof(MyEvent), Conventions.EndpointNamingConvention(typeof(Publisher)));
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