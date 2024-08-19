namespace NServiceBus.Transport.AzureStorageQueues.AcceptanceTests
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using Azure.Transports.WindowsAzureStorageQueues;
    using global::Azure.Storage.Queues;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NServiceBus.Faults;
    using NUnit.Framework;

    public class When_receiving_large_message : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_consume_it_without_the_error_headers_when_message_size_very_close_to_limit()
        {
            var ctx = await Scenario.Define<Context>()
                .WithEndpoint<Receiver>(b =>
                {
                    b.When((bus, c) =>
                    {
                        var connectionString = Testing.Utilities.GetEnvConfiguredConnectionString();
                        var queueClient = new QueueClient(connectionString, "receivinglargemessage-receiver");

                        //This value is fine tuned to ensure adding the 2 error headers make the message too large
                        string contentCloseToLimits = new string('x', (35 * 1024) + 425);

                        var message = new MyMessage { SomeProperty = contentCloseToLimits, };
                        var messageSerialized = JsonSerializer.SerializeToUtf8Bytes(message);

                        string id = Guid.NewGuid().ToString();
                        var wrapper = new MessageWrapper
                        {
                            Id = id,
                            Body = messageSerialized,
                            Headers = new Dictionary<string, string>
                            {
                                { Headers.EnclosedMessageTypes, $"{typeof(MyMessage).AssemblyQualifiedName}" },
                                { Headers.MessageId, id },
                                { Headers.CorrelationId, id },
                                {TestIndependence.HeaderName, c.TestRunId.ToString()}
                            }
                        };

                        var wrapperSerialized = JsonSerializer.SerializeToUtf8Bytes(wrapper);
                        var base64Encoded = Convert.ToBase64String(wrapperSerialized);

                        return queueClient.SendMessageAsync(base64Encoded);
                    }).DoNotFailOnErrorMessages();
                })
                .WithEndpoint<ErrorSpy>()
                .Done(c => c.MessageMovedToTheErrorQueue)
                .Run();

            Assert.Multiple(() =>
            {
                Assert.That(ctx.IsFailedQHeaderPresent, Is.False, "IsFailedQHeaderPresent");
                Assert.That(ctx.IsExceptionTypeHeaderPresent, Is.False, "IsExceptionTypeHeaderPresent");
            });
        }

        [Test]
        public async Task Should_consume_it_with_only_two_error_headers_when_message_size_close_to_limit()
        {
            var ctx = await Scenario.Define<Context>()
                .WithEndpoint<Receiver>(b =>
                {
                    b.When((bus, c) =>
                    {
                        var connectionString = Testing.Utilities.GetEnvConfiguredConnectionString();
                        var queueClient = new QueueClient(connectionString, "receivinglargemessage-receiver");

                        string contentCloseToLimits = new string('x', (35 * 1024) + 400);

                        var message = new MyMessage { SomeProperty = contentCloseToLimits, };
                        var messageSerialized = JsonSerializer.SerializeToUtf8Bytes(message);

                        string id = Guid.NewGuid().ToString();
                        var wrapper = new MessageWrapper
                        {
                            Id = id,
                            Body = messageSerialized,
                            Headers = new Dictionary<string, string>
                            {
                         { Headers.EnclosedMessageTypes, $"{typeof(MyMessage).AssemblyQualifiedName}" },
                         { Headers.MessageId, id },
                         { Headers.CorrelationId, id },
                         {TestIndependence.HeaderName, c.TestRunId.ToString()}
                            }
                        };

                        var wrapperSerialized = JsonSerializer.SerializeToUtf8Bytes(wrapper);
                        var base64Encoded = Convert.ToBase64String(wrapperSerialized);

                        return queueClient.SendMessageAsync(base64Encoded);
                    }).DoNotFailOnErrorMessages();
                })
                .WithEndpoint<ErrorSpy>()
                .Done(c => c.MessageMovedToTheErrorQueue)
                .Run();

            Assert.Multiple(() =>
            {
                Assert.That(ctx.IsFailedQHeaderPresent, Is.True, "IsFailedQHeaderPresent");
                Assert.That(ctx.IsExceptionTypeHeaderPresent, Is.True, "IsExceptionTypeHeaderPresent");
            });
        }

        class Context : ScenarioContext
        {
            public bool MessageMovedToTheErrorQueue { get; set; }
            public bool IsFailedQHeaderPresent { get; set; }
            public bool IsExceptionTypeHeaderPresent { get; set; }

        }

        class Receiver : EndpointConfigurationBuilder
        {
            public Receiver() => EndpointSetup<DefaultServer>(c =>
            {
                c.SendFailedMessagesTo(Conventions.EndpointNamingConvention(typeof(ErrorSpy)));
            });

            public class MyHandler : IHandleMessages<MyMessage>
            {
                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    throw new InvalidOperationException();
                }
            }
        }

        class ErrorSpy : EndpointConfigurationBuilder
        {
            public ErrorSpy() => EndpointSetup<DefaultServer>(config =>
            {
                config.LimitMessageProcessingConcurrencyTo(1);
            });

            class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public MyMessageHandler(Context testContext) => this.testContext = testContext;

                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    if (context.MessageHeaders.TryGetValue(TestIndependence.HeaderName, out var testRunId)
                        && testRunId == testContext.TestRunId.ToString())
                    {
                        testContext.MessageMovedToTheErrorQueue = true;
                    }
                    if (context.MessageHeaders.ContainsKey(FaultsHeaderKeys.FailedQ)
                        && testRunId == testContext.TestRunId.ToString())
                    {
                        testContext.IsFailedQHeaderPresent = true;
                    }
                    if (context.MessageHeaders.ContainsKey("NServiceBus.ExceptionInfo.ExceptionType")
                        && testRunId == testContext.TestRunId.ToString())
                    {
                        testContext.IsExceptionTypeHeaderPresent = true;
                    }

                    return Task.CompletedTask;
                }

                readonly Context testContext;
            }
        }

        public class MyMessage : IMessage
        {
            public string SomeProperty { get; set; }
        }
    }
}