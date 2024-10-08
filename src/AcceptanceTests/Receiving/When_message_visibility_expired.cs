﻿namespace NServiceBus.Transport.AzureStorageQueues.AcceptanceTests
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
                .WithEndpoint<Receiver>(b =>
                {
                    b.CustomConfig(c =>
                    {
                        // Limiting the concurrency for this test to make sure messages that are made available again are 
                        // not concurrently processed. This is not necessary for the test to pass but it makes
                        // reasoning about the test easier.
                        c.LimitMessageProcessingConcurrencyTo(1);
                    });
                    b.When((session, _) => session.SendLocal(new MyMessage()));
                })
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

                        // Limiting the concurrency for this test to make sure messages that are made available again are 
                        // not concurrently processed. This is not necessary for the test to pass but it makes
                        // reasoning about the test easier.
                        c.LimitMessageProcessingConcurrencyTo(1);
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
            public Receiver() => EndpointSetup<DefaultServer>(c =>
            {
                var transport = c.ConfigureTransport<AzureStorageQueueTransport>();
                // Explicitly setting the transport transaction mode to ReceiveOnly because the message 
                // tracking only is implemented for this mode.
                transport.TransportTransactionMode = TransportTransactionMode.ReceiveOnly;
            });
        }

        public class MyMessage : IMessage;

        class MyMessageHandler(Context testContext) : IHandleMessages<MyMessage>
        {
            public async Task Handle(MyMessage message, IMessageHandlerContext context)
            {
                var receiverQueue = BackwardsCompatibleQueueNameSanitizerForTests.Sanitize(Conventions.EndpointNamingConvention(typeof(Receiver)));
                var queueClient = new QueueClient(Utilities.GetEnvConfiguredConnectionString(), receiverQueue);
                var rawMessage = context.Extensions.Get<QueueMessage>();
                // By setting the visibility timeout zero, the message will be "immediately available" for retrieval again and effectively the message pump
                // has lost the message visibility timeout because any ACK or NACK will be rejected by the storage service.
                await queueClient.UpdateMessageAsync(rawMessage.MessageId, rawMessage.PopReceipt, visibilityTimeout: TimeSpan.Zero, cancellationToken: context.CancellationToken);

                testContext.MessageId = context.MessageId;

                if (testContext.ShouldThrow)
                {
                    throw new InvalidOperationException("Simulated exception");
                }
            }
        }
    }
}