namespace NServiceBus.Transport.AzureStorageQueues.AcceptanceTests
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Azure.Transports.WindowsAzureStorageQueues;
    using global::Azure.Storage.Queues;
    using global::Newtonsoft.Json;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;
    using Pipeline;

    public class When_receiving_large_message : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task ShouldConsumeIt()
        {
            var ctx = await Scenario.Define<Context>()
                .WithEndpoint<Receiver>(b =>
                {
                    b.CustomConfig((cfg, context) =>
                    {
                        cfg.UseSerialization<NewtonsoftSerializer>();
                    });

                    b.When((bus, c) =>
                    {
                        var connectionString = Testing.Utilities.GetEnvConfiguredConnectionString();
                        var queueClient = new QueueClient(connectionString, "receivinglargemessage-receiver");

                        string contentCloseToLimits = new string('x', 35 * 1024);

                        var message = new MyMessage { SomeProperty = contentCloseToLimits, };

                        var messageSerialized = JsonConvert.SerializeObject(message, typeof(MyMessage), Formatting.Indented, new JsonSerializerSettings());

                        string id = Guid.NewGuid().ToString();
                        var wrapper = new MessageWrapper
                        {
                            Id = id,
                            Body = Encoding.UTF8.GetBytes(messageSerialized),
                            Headers = new Dictionary<string, string>
                            {
                                { Headers.EnclosedMessageTypes, $"{typeof(MyMessage).AssemblyQualifiedName}" },
                                { Headers.MessageId, id },
                                { Headers.CorrelationId, id },
                                {TestIndependence.HeaderName, c.TestRunId.ToString()}
                            }
                        };

                        var wrapperSerialized = JsonConvert.SerializeObject(wrapper, typeof(MessageWrapper), Formatting.Indented, new JsonSerializerSettings());

                        var base64Encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(wrapperSerialized));

                        return queueClient.SendMessageAsync(base64Encoded);
                    });
                })
                .Done(c => c.GotMessage)
                .Run();

            Assert.True(ctx.GotMessage);
        }

        class Context : ScenarioContext
        {
            public bool GotMessage { get; set; }
        }

        class Receiver : EndpointConfigurationBuilder
        {
            public Receiver() => EndpointSetup<DefaultServer>(c =>
            {
                c.AuditProcessedMessagesTo("audit");
            });
        }

        public class MyHandler : IHandleMessages<MyMessage>
        {
            public Task Handle(MyMessage message, IMessageHandlerContext context)
            {
                throw new InvalidOperationException();
            }
        }

        public class MyMessage : IMessage
        {
            public string SomeProperty { get; set; }
        }
    }
}