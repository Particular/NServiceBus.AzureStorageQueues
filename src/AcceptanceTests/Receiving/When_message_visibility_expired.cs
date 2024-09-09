namespace NServiceBus.Transport.AzureStorageQueues.AcceptanceTests
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using global::Azure.Storage.Queues;
    using global::Azure.Storage.Queues.Models;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;
    using Testing;

    public class When_message_visibility_expired : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_complete_message_on_next_receive_when_pipeline_successful()
        {
            var ctx = await Scenario.Define<Context>()
                .WithEndpoint<Receiver>(b => b.When((session, _) => session.SendLocal(new MyMessage())))
                .Done(c => c.MessageId is not null && c.Logs.Any(l => WasMarkedAsSuccessfullyCompleted(l, c)))
                .Run();

            var items = ctx.Logs.Where(l => WasMarkedAsSuccessfullyCompleted(l, ctx)).ToArray();

            Assert.That(items, Is.Not.Empty);
        }

        [Test]
        public async Task Should_complete_message_on_next_receive_when_error_pipeline_handled_the_message()
        {
            var ctx = await Scenario.Define<Context>(c =>
                {
                    c.ShouldThrow = true;
                })
                .WithEndpoint<Receiver>(b =>
                {
                    b.DoNotFailOnErrorMessages();
                    b.CustomConfig(c =>
                    {
                        var recoverability = c.Recoverability();
                        recoverability.AddUnrecoverableException<InvalidOperationException>();
                    });
                    b.When((session, _) => session.SendLocal(new MyMessage()));
                })
                .Done(c => c.MessageId is not null && c.Logs.Any(l => WasMarkedAsSuccessfullyCompleted(l, c)))
                .Run();

            var items = ctx.Logs.Where(l => WasMarkedAsSuccessfullyCompleted(l, ctx)).ToArray();

            Assert.That(items, Is.Not.Empty);
        }

        static bool WasMarkedAsSuccessfullyCompleted(ScenarioContext.LogItem l, Context c)
            => l.Message.StartsWith($"Received message (ID: '{c.MessageId}') was marked as successfully completed");

        class Context : ScenarioContext
        {
            public bool ShouldThrow { get; set; }

            public string MessageId { get; set; }
        }

        class Receiver : EndpointConfigurationBuilder
        {
            public Receiver() => EndpointSetup<DefaultServer>();
        }

        public class MyMessage : IMessage;

        class MyMessageHandler(Context testContext) : IHandleMessages<MyMessage>
        {
            public async Task Handle(MyMessage message, IMessageHandlerContext context)
            {
                var receiverQueue = BackwardsCompatibleQueueNameSanitizerForTests.Sanitize(Conventions.EndpointNamingConvention(typeof(Receiver)));
                var queueClient = new QueueClient(Utilities.GetEnvConfiguredConnectionString(), receiverQueue);
                var rawMessage = context.Extensions.Get<QueueMessage>();
                // By setting the visibility timeout to a second, the message will be "immediately available" for retrieval again and effectively the message pump
                // has lost the message visibility timeout because any ACK or NACK will be rejected by the storage service. The second was chosen
                // to make sure there is some gap between the message is available again because the transport does heavy polling and concurrent fetching by default.
                await queueClient.UpdateMessageAsync(rawMessage.MessageId, rawMessage.PopReceipt, visibilityTimeout: TimeSpan.FromSeconds(1), cancellationToken: context.CancellationToken);

                testContext.MessageId = context.MessageId;

                if (testContext.ShouldThrow)
                {
                    throw new InvalidOperationException("Simulated exception");
                }
            }
        }
    }
}