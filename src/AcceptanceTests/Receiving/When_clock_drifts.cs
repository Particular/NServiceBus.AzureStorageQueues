namespace NServiceBus.Transport.AzureStorageQueues.AcceptanceTests
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_receiving_a_message_and_clock_drifts : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_acknowledge_the_message()
        {
            var context = await Scenario.Define<Context>()
                .WithEndpoint<Receiver>(b =>
                {
                    b.When(async (bus, c) => { await bus.SendLocal(new KickOffMessage()); });
                })
                .Done(c => c.MessageReceived)
                .Run();

            var ackFailures = context.Logs.Where(l =>
                l.Message.StartsWith("Dispatching the message took longer than a visibility timeout. The message will reappear in the queue and will be obtained again"));

            Assert.That(ackFailures, Is.Empty, "The message was not successfully acknowledged");
        }

        class Context : ScenarioContext
        {
            public bool MessageReceived { get; set; }
        }

        class Receiver : EndpointConfigurationBuilder
        {
            public Receiver() =>
                EndpointSetup<DefaultServer>(config =>
                {
                    var transport = config.ConfigureTransport<AzureStorageQueueTransport>();
                    transport.TimeProvider = new FakeTimeProvider();
                });
        }

        class FakeTimeProvider : TimeProvider
        {
            // simulating a clock drift of 60 seconds
            public override DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow.Add(TimeSpan.FromSeconds(60));
        }

        public class KickOffMessage : IMessage
        {
        }

        public class NextMessage : IMessage
        {
        }

        class KickOffMessageHandler : IHandleMessages<KickOffMessage>
        {
            public Task Handle(KickOffMessage message, IMessageHandlerContext context) => context.SendLocal(new NextMessage());
        }

        class NextMessageHandler(Context testContext) : IHandleMessages<NextMessage>
        {
            public Task Handle(NextMessage message, IMessageHandlerContext context)
            {
                testContext.MessageReceived = true;
                return Task.CompletedTask;
            }
        }
    }
}