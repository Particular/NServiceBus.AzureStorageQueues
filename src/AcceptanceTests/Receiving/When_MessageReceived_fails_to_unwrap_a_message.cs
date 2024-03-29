﻿namespace NServiceBus.Transport.AzureStorageQueues.AcceptanceTests
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NUnit.Framework;
    using Testing;

    public class When_MessageReceived_fails_to_unwrap_a_message : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_move_message_to_the_error_queue()
        {
            var context = await Scenario.Define<Context>()
                .WithEndpoint<Receiver>(b =>
                {
                    b.When(async (e, c) =>
                    {
                        var message = new MyMessage
                        {
                            Id = c.TestRunId
                        };

                        await e.SendLocal(message);
                    });
                })
                .WithEndpoint<ErrorSpy>()
                .Done(c => c.MessageMovedToErrorQueue)
                .Run();

            var exceptionThrownByUnwrapper = context.Logs.Any(x => x.Message.StartsWith("Failed to deserialize message envelope for message with id"));
            Assert.That(exceptionThrownByUnwrapper, "Exception thrown by MessageRetrieved.Unwrap() was expected but wasn't found");
        }

        class Context : ScenarioContext
        {
            public bool GotMessage { get; set; }
            public bool MessageMovedToErrorQueue { get; set; }
        }

        class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                var transport = new AzureStorageQueueTransport(Utilities.GetEnvConfiguredConnectionString(), useNativeDelayedDeliveries: false)
                {
                    MessageUnwrapper = message => throw new Exception("Custom unwrapper failed"),
                    QueueNameSanitizer = BackwardsCompatibleQueueNameSanitizerForTests.Sanitize
                };

                EndpointSetup(new CustomizedServer(transport), (config, rd) =>
                {
                    config.SendFailedMessagesTo(AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(ErrorSpy)));
                    config.LimitMessageProcessingConcurrencyTo(1);
                });
            }
        }

        class ErrorSpy : EndpointConfigurationBuilder
        {
            public ErrorSpy()
            {
                var transport = new AzureStorageQueueTransport(Utilities.GetEnvConfiguredConnectionString(), useNativeDelayedDeliveries: false)
                {
                    QueueNameSanitizer = BackwardsCompatibleQueueNameSanitizerForTests.Sanitize
                };

                EndpointSetup(new CustomizedServer(transport), (config, rd) => { });
            }

            class MyMessageHandler : IHandleMessages<MyMessage>
            {
                Context testContext;

                public MyMessageHandler(Context testContext) => this.testContext = testContext;

                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    if (message.Id == testContext.TestRunId)
                    {
                        testContext.MessageMovedToErrorQueue = true;
                    }

                    return Task.CompletedTask;
                }
            }
        }

        public class MyMessage : IMessage
        {
            public Guid Id { get; set; }
        }
    }
}