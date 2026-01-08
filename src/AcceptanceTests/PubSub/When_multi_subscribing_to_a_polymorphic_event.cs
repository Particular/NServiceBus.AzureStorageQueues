namespace NServiceBus.Transport.AzureStorageQueues.AcceptanceTests.PubSub
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_multi_subscribing_to_a_polymorphic_event : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Both_events_should_be_delivered()
        {
            Requires.NativePubSubSupport();

            var context = await Scenario.Define<Context>()
                .WithEndpoint<Publisher1>(b => b.When((session, c) =>
                {
                    c.AddTrace("Publishing MyEvent1");
                    return session.Publish(new MyEvent1());
                }))
                .WithEndpoint<Publisher2>(b => b.When((session, c) =>
                {
                    c.AddTrace("Publishing MyEvent2");
                    return session.Publish(new MyEvent2());
                }))
                .WithEndpoint<Subscriber>()
                .Done(c => c.SubscriberGotIMyEvent && c.SubscriberGotMyEvent2)
                .Run();

            Assert.Multiple(() =>
            {
                Assert.That(context.SubscriberGotIMyEvent, Is.True);
                Assert.That(context.SubscriberGotMyEvent2, Is.True);
            });
        }

        public class Context : ScenarioContext
        {
            public bool SubscriberGotIMyEvent { get; set; }
            public bool SubscriberGotMyEvent2 { get; set; }

            public void MaybeMarkAsCompleted() => MarkAsCompleted(SubscriberGotIMyEvent, SubscriberGotMyEvent2);
        }

        public class Publisher1 : EndpointConfigurationBuilder
        {
            public Publisher1() => EndpointSetup<DefaultPublisher>();
        }

        public class Publisher2 : EndpointConfigurationBuilder
        {
            public Publisher2() => EndpointSetup<DefaultPublisher>();
        }

        public class Subscriber : EndpointConfigurationBuilder
        {
            public Subscriber() => EndpointSetup<DefaultServer>();

            public class MyHandler : IHandleMessages<IMyEvent>
            {
                readonly Context testContext;

                public MyHandler(Context testContext) => this.testContext = testContext;

                public Task Handle(IMyEvent messageThatIsEnlisted, IMessageHandlerContext context)
                {
                    testContext.AddTrace($"Got event '{messageThatIsEnlisted}'");
                    if (messageThatIsEnlisted is MyEvent2)
                    {
                        testContext.SubscriberGotMyEvent2 = true;
                    }
                    else
                    {
                        testContext.SubscriberGotIMyEvent = true;
                    }

                    testContext.MaybeMarkAsCompleted();
                    return Task.CompletedTask;
                }
            }
        }

        public class MyEvent1 : IMyEvent;

        public class MyEvent2 : IMyEvent;

        public interface IMyEvent : IEvent;
    }
}