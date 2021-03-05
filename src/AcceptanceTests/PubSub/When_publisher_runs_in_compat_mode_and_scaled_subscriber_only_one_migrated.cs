namespace NServiceBus.Transport.AzureStorageQueues.AcceptanceTests.PubSub
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using Configuration.AdvancedExtensibility;
    using Features;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;
    using Routing.MessageDrivenSubscriptions;

    public class When_publisher_runs_in_compat_mode_and_scaled_subscriber_only_one_migrated : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task It_should_still_deliver_once()
        {
            var context = await Scenario.Define<Context>()
                .WithEndpoint<Publisher>(b => b.When(c => c.Instance1SubscribedMessageDriven && c.Instance2SubscribedNative, (session, ctx) => session.Publish(new MyEvent())))
                .WithEndpoint(new Subscriber(false),b =>
                {
                    b.CustomConfig(c =>
                    {
                        c.GetSettings().GetOrCreate<Publishers>().AddOrReplacePublishers("LegacyConfig", new List<PublisherTableEntry>
                        {
                            new PublisherTableEntry(typeof(MyEvent), PublisherAddress.CreateFromEndpointName(Conventions.EndpointNamingConvention(typeof(Publisher))))
                        });
                    });
                    b.When(async (session, ctx) =>
                    {
                        await session.Subscribe<MyEvent>();
                    });
                })
                .WithEndpoint(new Subscriber(true),b => b.When(async (session, ctx) =>
                {
                    await session.Subscribe<MyEvent>();
                    ctx.Instance2SubscribedNative = true;
                }))
                .Done(c => c.EventReceived >= 1)
                .Run();

            Assert.AreEqual(1, context.EventReceived);
        }

        public class Context : ScenarioContext
        {
            long eventReceived;

            public bool Instance1SubscribedMessageDriven { get; set; }
            public bool Instance2SubscribedNative { get; set; }

            public long EventReceived => Interlocked.Read(ref eventReceived);

            public void GotTheEvent() => Interlocked.Increment(ref eventReceived);
        }

        public class Publisher : EndpointConfigurationBuilder
        {
            public Publisher()
            {
                EndpointSetup<DefaultPublisher>(c =>
                {
                    c.ConfigureRouting().EnableMessageDrivenPubSubCompatibilityMode();

                    c.OnEndpointSubscribed<Context>((s, context) =>
                    {
                        if (s.SubscriberEndpoint.Contains(Conventions.EndpointNamingConvention(typeof(Subscriber))))
                        {
                            context.Instance1SubscribedMessageDriven = true;
                        }
                    });
                }).IncludeType<TestingInMemorySubscriptionPersistence>();
            }
        }

        public class Subscriber : EndpointConfigurationBuilder
        {
            public Subscriber(bool supportsPublishSubscribe)
            {
                EndpointSetup(new CustomizedServer(supportsNativeDelayedDelivery: true, supportsPublishSubscribe), (c, rd) =>
                    {
                        c.DisableFeature<AutoSubscribe>();
                    },
                    metadata => metadata.RegisterPublisherFor<MyEvent>(typeof(Publisher)));
            }

            public class MyEventHandler : IHandleMessages<MyEvent>
            {
                readonly Context testContext;

                public MyEventHandler(Context testContext) => this.testContext = testContext;

                public Task Handle(MyEvent @event, IMessageHandlerContext context)
                {
                    testContext.GotTheEvent();
                    return Task.CompletedTask;
                }
            }
        }

        public class MyEvent : IEvent
        {
        }
    }
}