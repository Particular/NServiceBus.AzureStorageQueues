namespace NServiceBus.Transport.AzureStorageQueues.AcceptanceTests.DelayedDelivery
{
    using System;
    using System.Buffers.Text;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using global::Azure.Storage.Queues;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NServiceBus.Azure.Transports.WindowsAzureStorageQueues;
    using NUnit.Framework;
    using Testing;

    class Native_message_delayed_retry : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_handle_delayed_delivery_of_native_message()
        {
            var ctx = await Scenario.Define<MyContext>()
                .WithEndpoint<SampleEndpoint>(endpoint => endpoint
                    .DoNotFailOnErrorMessages()
                    .When(async session =>
                    {
                        var nativeMessage = new NativeMessage
                        {
                            Content = $"Hello from native sender @ {DateTimeOffset.UtcNow}"
                        };

                        var queueClient = new QueueClient(Utilities.GetEnvConfiguredConnectionString(), "native-integration-asq");
                        await queueClient.CreateIfNotExistsAsync();

                        var serializedMessage = JsonSerializer.Serialize(nativeMessage);
                        await queueClient.SendMessageAsync(serializedMessage);
                    })
                )
                .Run();

            Assert.That(ctx.IsDone, Is.True);
        }

        class SampleEndpoint : EndpointConfigurationBuilder
        {
            public SampleEndpoint() =>
                EndpointSetup<DefaultServer>(
                    cfg =>
                    {
                        cfg.Recoverability()
                            .Delayed(delayed => delayed.NumberOfRetries(1).TimeIncrease(TimeSpan.FromSeconds(1)))
                            .Immediate(immediate => immediate.NumberOfRetries(0));
                        var transport = cfg.ConfigureTransport<AzureStorageQueueTransport>();
                        transport.MessageUnwrapper = message =>
                        {
                            return Base64.IsValid(message.MessageText)
                            ? null
                            : new MessageWrapper
                            {
                                Id = message.MessageId,
                                Body = message.Body.ToArray(),
                                Headers = new Dictionary<string, string>
                                {
                                    { Headers.EnclosedMessageTypes, typeof(NativeMessage).FullName },
                                    { TestIndependence.HeaderName, ScenarioContext.TestRunId.ToString() }
                                }
                            };
                        };
                    })
                .CustomEndpointName("native-integration-asq");

            class MyMessageHandler(MyContext scenarioContext) : IHandleMessages<NativeMessage>
            {
                public Task Handle(NativeMessage message, IMessageHandlerContext context)
                {
                    if (context.MessageHeaders.GetValueOrDefault(Headers.DelayedRetries) == "1")
                    {
                        scenarioContext.IsDone = true;
                        scenarioContext.MarkAsCompleted();
                    }
                    throw new Exception("Failing over to delay retry");
                }
            }
        }

        class NativeMessage : IMessage
        {
            public string Content { get; set; }
        }

        class MyContext : ScenarioContext
        {
            public bool IsDone { get; set; }
        }
    }
}
