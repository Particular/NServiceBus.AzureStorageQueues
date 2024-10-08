namespace NServiceBus.Transport.AzureStorageQueues.AcceptanceTests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Support;
    using Configuration.AdvancedExtensibility;
    using Features;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NServiceBus.Routing.MessageDrivenSubscriptions;
    using NUnit.Framework;
    using Conventions = AcceptanceTesting.Customization.Conventions;

    public class When_migrating_subscriber_first : NServiceBusAcceptanceTest
    {
        static string PublisherEndpoint => Conventions.EndpointNamingConvention(typeof(Publisher));

        [Test]
        public async Task Should_not_lose_any_events()
        {
            var subscriptionStorage = new TestingInMemorySubscriptionStorage();

            //Before migration begins
            var beforeMigration = await Scenario.Define<Context>()
                .WithEndpoint(new Publisher(supportsPublishSubscribe: false), b =>
                 {
                     b.CustomConfig(c =>
                     {
                         c.UsePersistence<TestingInMemoryPersistence, StorageType.Subscriptions>().UseStorage(subscriptionStorage);
                     });
                     b.When(c => c.SubscribedMessageDriven, (session, ctx) => session.Publish(new MyEvent()));
                 })

                .WithEndpoint(new Subscriber(supportsPublishSubscribe: false), b =>
                 {
                     b.CustomConfig(c =>
                     {
                         c.GetSettings().GetOrCreate<Publishers>().AddOrReplacePublishers("LegacyConfig",
                         [
                            new PublisherTableEntry(typeof(MyEvent), PublisherAddress.CreateFromEndpointName(PublisherEndpoint))
                         ]);
                     });
                     b.When(async (session, ctx) =>
                     {
                         await session.Subscribe<MyEvent>();
                     });
                 })
                .Done(c => c.GotTheEvent)
                .Run(TimeSpan.FromSeconds(30));

            Assert.That(beforeMigration.GotTheEvent, Is.True);

            //Subscriber migrated and in compatibility mode.
            var subscriberMigratedRunSettings = new RunSettings
            {
                TestExecutionTimeout = TimeSpan.FromSeconds(30)
            };
            var subscriberMigrated = await Scenario.Define<Context>()
                .WithEndpoint(new Publisher(supportsPublishSubscribe: false), b =>
                 {
                     b.CustomConfig(c =>
                     {
                         c.UsePersistence<TestingInMemoryPersistence, StorageType.Subscriptions>().UseStorage(subscriptionStorage);
                     });
                     b.When(c => c.SubscribedMessageDriven, (session, ctx) => session.Publish(new MyEvent()));
                 })

                .WithEndpoint(new Subscriber(supportsPublishSubscribe: true), b =>
                 {
                     b.CustomConfig(c =>
                     {
                         var compatModeSettings = c.ConfigureRouting().EnableMessageDrivenPubSubCompatibilityMode();
                         compatModeSettings.RegisterPublisher(typeof(MyEvent), PublisherEndpoint);
                     });
                     b.When(async (session, ctx) =>
                     {
                         //Subscribes both using native feature and message-driven
                         await session.Subscribe<MyEvent>();
                         ctx.SubscribedNative = true;
                     });
                 })
                .Done(c => c.GotTheEvent && c.SubscribedNative) //we ensure the subscriber did subscriber with the native mechanism
                .Run(subscriberMigratedRunSettings);

            Assert.That(subscriberMigrated.GotTheEvent, Is.True);

            //Publisher migrated and in compatibility mode
            var publisherMigratedRunSettings = new RunSettings
            {
                TestExecutionTimeout = TimeSpan.FromSeconds(30)
            };
            var publisherMigrated = await Scenario.Define<Context>()
                .WithEndpoint(new Publisher(supportsPublishSubscribe: true), b =>
                 {
                     b.CustomConfig(c =>
                     {
                         c.UsePersistence<TestingInMemoryPersistence, StorageType.Subscriptions>().UseStorage(subscriptionStorage);
                         c.ConfigureRouting().EnableMessageDrivenPubSubCompatibilityMode();
                     });
                     b.When(c => c.SubscribedMessageDriven && c.SubscribedNative, (session, ctx) => session.Publish(new MyEvent()));
                 })

                .WithEndpoint(new Subscriber(supportsPublishSubscribe: true), b =>
                 {
                     b.CustomConfig(c =>
                     {
                         var compatModeSettings = c.ConfigureRouting().EnableMessageDrivenPubSubCompatibilityMode();
                         // not needed but left here to enforce duplicates
                         compatModeSettings.RegisterPublisher(typeof(MyEvent), PublisherEndpoint);
                     });
                     b.When(async (session, ctx) =>
                     {
                         await session.Subscribe<MyEvent>();
                         ctx.SubscribedNative = true;
                     });
                 })
                .Done(c => c.GotTheEvent)
                .Run(publisherMigratedRunSettings);

            Assert.That(publisherMigrated.GotTheEvent, Is.True);

            //Compatibility mode disabled in both publisher and subscriber
            var compatModeDisabled = await Scenario.Define<Context>()
                .WithEndpoint(new Publisher(supportsPublishSubscribe: true), b =>
                 {
                     b.When(c => c.EndpointsStarted, (session, ctx) => session.Publish(new MyEvent()));
                 })
                .WithEndpoint(new Subscriber(supportsPublishSubscribe: true), c => { })
                .Done(c => c.GotTheEvent)
                .Run(TimeSpan.FromSeconds(30));

            Assert.That(compatModeDisabled.GotTheEvent, Is.True);
        }

        public class Context : ScenarioContext
        {
            public bool GotTheEvent { get; set; }
            public bool SubscribedMessageDriven { get; set; }
            public bool SubscribedNative { get; set; }
        }

        public class Publisher : EndpointConfigurationBuilder
        {
            public Publisher(bool supportsPublishSubscribe) =>
                EndpointSetup(new CustomizedServer(supportsNativeDelayedDelivery: true, supportsPublishSubscribe), (c, rd) =>
                {
                    c.OnEndpointSubscribed<Context>((s, context) =>
                    {
                        if (s.SubscriberEndpoint.Contains(Conventions.EndpointNamingConvention(typeof(Subscriber))))
                        {
                            context.SubscribedMessageDriven = true;
                        }
                    });
                }).IncludeType<TestingInMemorySubscriptionPersistence>();
        }

        public class Subscriber : EndpointConfigurationBuilder
        {
            public Subscriber(bool supportsPublishSubscribe) =>
                EndpointSetup(new CustomizedServer(supportsNativeDelayedDelivery: true, supportsPublishSubscribe), (c, rd) =>
                    {
                        c.DisableFeature<AutoSubscribe>();
                    },
                    metadata => metadata.RegisterPublisherFor<MyEvent>(typeof(Publisher)));

            public class MyHandler : IHandleMessages<MyEvent>
            {
                readonly Context testContext;

                public MyHandler(Context testContext) => this.testContext = testContext;

                public Task Handle(MyEvent @event, IMessageHandlerContext context)
                {
                    testContext.GotTheEvent = true;
                    return Task.CompletedTask;
                }
            }
        }

        public class MyEvent : IEvent
        {
        }
    }
}