namespace NServiceBus.Azure.Transports.WindowsAzureStorageQueues.AcceptanceTests.Receiving
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using global::Azure.Storage.Queues;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;
    using System;
    using System.Text;

    public class When_receiving_from_v8_endpoint : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_not_fail()
        {
            await Scenario.Define<Context>()
                .WithEndpoint<Receiver>(b =>
                {
                    b.When(async (session, context) =>
                    {
                        var connectionString = Testing.Utilities.GetEnvConfiguredConnectionString();
                        var endpointName = AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(Receiver));
                        var sanitizedEndpointName = BackwardsCompatibleQueueNameSanitizerForTests.Sanitize(endpointName);
                        var queueClient = new QueueClient(connectionString, sanitizedEndpointName);
                        var messageType = typeof(MyMessage).FullName;
                        var message = Convert.ToBase64String(Encoding.UTF8.GetBytes(
                            $"{{\"IdForCorrelation\":null,\"Id\":\"5bdd49bc-edd9-41c2-ae84-ac6e007092a6\",\"MessageIntent\":1,\"ReplyToAddress\":\"samples-azure-storagequeues-endpoint1@{connectionString}\",\"Headers\":{{\"$AcceptanceTesting.TestRunId\":\"{context.TestRunId}\",\"NServiceBus.MessageId\":\"5bdd49bc-edd9-41c2-ae84-ac6e007092a6\",\"NServiceBus.MessageIntent\":\"Send\",\"NServiceBus.ConversationId\":\"37ee8af6-c51b-4906-8988-ac6e007092a6\",\"NServiceBus.CorrelationId\":\"5bdd49bc-edd9-41c2-ae84-ac6e007092a6\",\"NServiceBus.ReplyToAddress\":\"samples-azure-storagequeues-endpoint1@{connectionString}\",\"NServiceBus.OriginatingMachine\":\"BEAST\",\"NServiceBus.OriginatingEndpoint\":\"Samples-Azure-StorageQueues-Endpoint1\",\"$.diagnostics.originating.hostid\":\"a909953da6ab9150185d95c0acfd7125\",\"NServiceBus.ContentType\":\"application/json\",\"NServiceBus.EnclosedMessageTypes\":\"{messageType}\",\"NServiceBus.TimeSent\":\"2020-11-09 06:49:51:912730 Z\"}},\"Body\":\"77u/eyJQcm9wZXJ0eSI6IkhlbGxvIGZyb20gRW5kcG9pbnQxIn0=\",\"CorrelationId\":\"5bdd49bc-edd9-41c2-ae84-ac6e007092a6\",\"Recoverable\":true}}"));
                        await queueClient.SendMessageAsync(message);
                    });
                })
                .Done(c => c.MessageReceived)
                .Run();
        }

        class Context : ScenarioContext
        {
            public bool MessageReceived { get; set; }
        }

        class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServer>(config =>
                {
                    config.UseSerialization<NewtonsoftSerializer>();

                    var transport = config.ConfigureAsqTransport();

                    transport.DelayedDelivery().DisableDelayedDelivery();
                });
            }

            class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public MyMessageHandler(Context testContext)
                {
                    this.testContext = testContext;
                }

                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    testContext.MessageReceived = true;

                    return Task.CompletedTask;
                }

                readonly Context testContext;
            }

        }

        public class MyMessage : IMessage
        {
        }
    }
}