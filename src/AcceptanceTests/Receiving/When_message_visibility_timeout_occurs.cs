namespace NServiceBus.Transport.AzureStorageQueues.AcceptanceTests
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    [TestFixture]
    public class When_message_visibility_timeout_occurs : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_acknowledge_the_message()
        {
            var context = await Scenario.Define<Context>()
                .WithEndpoint<Receiver>(b =>
                {
                    b.When(async (bus, c) => { await bus.SendLocal(new KickOffMessage()); });
                })
                .Done(c => c.MessageId != null)
                .Run();

            var ackFailures = context.Logs.Where(l =>
                l.Message.StartsWith("Dispatching the message took longer than a visibility timeout. The message will reappear in the queue and will be obtained again"));

            Assert.That(ackFailures, Is.Empty, "The message was not successfully acknowledged");
        }

        class Context : ScenarioContext
        {
            public int NumberOfTimesInvoked { get; set; }

            public string MessageId { get; set; }
        }

        class Receiver : EndpointConfigurationBuilder
        {
            public Receiver() =>
                EndpointSetup<DefaultServer>((config) =>
                {
                    var transport = config.ConfigureTransport<AzureStorageQueueTransport>();
                    transport.TimeProvider = new FakeTimeProvider();
                });
        }

        class FakeTimeProvider : TimeProvider
        {
            long timeStamp;

            public override long GetTimestamp()
            {
                long monotonicIncrement = 60 * TimestampFrequency;
                return Interlocked.Add(ref timeStamp, monotonicIncrement);
            }
        }

        public class KickOffMessage : IMessage
        {
        }

        class KickOffMessageHandler(Context testContext) : IHandleMessages<KickOffMessage>
        {
            public Task Handle(KickOffMessage message, IMessageHandlerContext context)
            {
                testContext.MessageId = context.MessageId;
                testContext.NumberOfTimesInvoked++;
                return Task.CompletedTask;
            }
        }
    }
}