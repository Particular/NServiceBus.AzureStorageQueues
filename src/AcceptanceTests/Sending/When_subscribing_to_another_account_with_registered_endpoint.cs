#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable CS0618 // Type or member is obsolete

namespace NServiceBus.Transport.AzureStorageQueues.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using Features;
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
                    var transport = new AzureStorageQueueTransport(Utilities.GetEnvConfiguredConnectionString())
                    {
                        QueueNameSanitizer = BackwardsCompatibleQueueNameSanitizerForTests.Sanitize
                    };
                    transport.AccountRouting.DefaultAccountAlias = DefaultAccountName;
                    var anotherAccount = transport.AccountRouting.AddAccount(AnotherAccountName, Utilities.GetEnvConfiguredConnectionString2());
                    anotherAccount.RegisteredEndpoints.Add(Conventions.EndpointNamingConvention(typeof(Subscriber)));

                    configuration.UseTransport(transport);

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

                    var transport = new AzureStorageQueueTransport(Utilities.GetEnvConfiguredConnectionString2())
                    {
                        QueueNameSanitizer = BackwardsCompatibleQueueNameSanitizerForTests.Sanitize
                    };
                    transport.AccountRouting.DefaultAccountAlias = AnotherAccountName;
                    var anotherAccount = transport.AccountRouting.AddAccount(DefaultAccountName, Utilities.GetEnvConfiguredConnectionString());
                    anotherAccount.RegisteredEndpoints.Add(Conventions.EndpointNamingConvention(typeof(Publisher)));

                    var routing = configuration.UseTransport(transport);
                    routing.RegisterPublisher(typeof(MyEvent), Conventions.EndpointNamingConvention(typeof(Publisher)));
                });
            }

            public class MyMessageHandler : IHandleMessages<MyEvent>
            {
                readonly Context _testContext;

                public MyMessageHandler(Context testContext)
                {
                    _testContext = testContext;
                }

                public Task Handle(MyEvent message, IMessageHandlerContext context)
                {
                    _testContext.WasCalled = true;
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

#pragma warning restore CS0618 // Type or member is obsolete
#pragma warning restore IDE0079 // Remove unnecessary suppression