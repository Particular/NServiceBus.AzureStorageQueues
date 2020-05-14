﻿namespace NServiceBus.Azure.Transports.WindowsAzureStorageQueues.AcceptanceTests.Receiving
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

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
                EndpointSetup<DefaultServer>(config =>
                {
                    config.SendFailedMessagesTo(AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(ErrorSpy)));
                    config.UseSerialization<NewtonsoftSerializer>();
                    config.LimitMessageProcessingConcurrencyTo(1);
                    var transport = config.ConfigureAsqTransport();
                    transport.UnwrapMessagesWith(message => throw new Exception("Custom unwrapper failed"));
                    transport.DelayedDelivery().DisableDelayedDelivery();
                });
            }
        }

        class ErrorSpy : EndpointConfigurationBuilder
        {
            public ErrorSpy()
            {
                EndpointSetup<DefaultServer>(config =>
                {
                    var transport = config.ConfigureAsqTransport();
                    config.UseSerialization<NewtonsoftSerializer>();
                    transport.DelayedDelivery().DisableDelayedDelivery();
                });
            }

            class MyMessageHandler : IHandleMessages<MyMessage>
            {
                readonly Context testContext;

                public MyMessageHandler(Context testContext)
                {
                    this.testContext = testContext;
                }

                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    if (message.Id == testContext.TestRunId)
                    {
                        testContext.MessageMovedToErrorQueue = true;
                    }

                    return Task.FromResult(0);
                }
            }
        }

        public class MyMessage : IMessage
        {
            public Guid Id { get; set; }
        }
    }
}