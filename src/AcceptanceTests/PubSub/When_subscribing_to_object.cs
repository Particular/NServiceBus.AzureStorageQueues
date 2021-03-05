namespace NServiceBus.AcceptanceTests.PubSub
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using EndpointTemplates;
    using NUnit.Framework;

    public class When_subscribing_to_object : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task It_should_still_deliver()
        {
            var context = await Scenario.Define<Context>()
                .WithEndpoint<Publisher>(b => b.When(c => c.EndpointsStarted, session => session.Publish(new MyEvent())))
                .WithEndpoint<Subscriber>(b => { })
                .Done(c => c.GotTheEvent)
                .Run();

            Assert.True(context.GotTheEvent);
        }

        public class Context : ScenarioContext
        {
            public bool GotTheEvent { get; set; }
        }

        public class Publisher : EndpointConfigurationBuilder
        {
            public Publisher()
            {
                EndpointSetup<DefaultPublisher>(c => { });
            }
        }

        public class Subscriber : EndpointConfigurationBuilder
        {
            public Subscriber()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.Conventions().DefiningEventsAs(t => typeof(object).IsAssignableFrom(t) || typeof(IEvent).IsAssignableFrom(t));
                });
            }

            public class MyEventHandler : IHandleMessages<object>
            {
                readonly Context testContext;

                public MyEventHandler(Context testContext) => this.testContext = testContext;

                public Task Handle(object @event, IMessageHandlerContext context)
                {
                    testContext.GotTheEvent = true;
                    return Task.FromResult(0);
                }
            }
        }

        public class MyEvent : IEvent
        {
        }
    }
}