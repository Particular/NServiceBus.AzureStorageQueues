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
    public class When_message_visibility_timeout_occurs_during_recoverability : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_do_the_configured_number_of_retries()
        {
            var context = await Scenario.Define<Context>()
                .WithEndpoint<Receiver>(b =>
                {
                    b.When(async (bus, c) => { await bus.SendLocal(new KickOffMessage()); })
                     .DoNotFailOnErrorMessages();
                })
                .Done(c => c.ForwardedToErrorQueue)
                .Run();

            Assert.True(context.ForwardedToErrorQueue);
            Assert.AreEqual(numberOfRetries + 1, context.NumberOfTimesInvoked, "Message should be retried 5 times immediately");
            Assert.That(context.Logs.Where(l => l.Message.StartsWith($"Failed to execute recoverability policy for message with native ID: `{context.MessageId}`")), Is.Empty, "Circuit breaker should not have been triggered.");
            Assert.AreEqual(numberOfRetries, context.Logs.Count(l => l.Message
                .StartsWith($"Immediate Retry is going to retry message '{context.MessageId}' because of an exception:")));
        }

        const int numberOfRetries = 5;

        class Context : ScenarioContext
        {
            public int NumberOfTimesInvoked { get; set; }

            public bool ForwardedToErrorQueue { get; set; }

            public string MessageId { get; set; }
        }

        class Receiver : EndpointConfigurationBuilder
        {
            public Receiver() =>
                EndpointSetup<DefaultServer>((config, context) =>
                {
                    var scenarioContext = (Context)context.ScenarioContext;

                    var recoverability = config.Recoverability();
                    recoverability.Failed(f => f.OnMessageSentToErrorQueue((message, _) =>
                    {
                        scenarioContext.ForwardedToErrorQueue = true;
                        return Task.CompletedTask;
                    }));
                    recoverability.Immediate(immediate => immediate.NumberOfRetries(numberOfRetries));

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
                throw new SimulatedException();
            }
        }
    }
}