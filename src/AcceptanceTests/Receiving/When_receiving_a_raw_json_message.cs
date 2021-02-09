namespace NServiceBus.Transport.AzureStorageQueues.AcceptanceTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using global::Azure.Storage.Queues;
    using global::Azure.Storage.Queues.Models;
    using global::Newtonsoft.Json;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NServiceBus.Azure.Transports.WindowsAzureStorageQueues;
    using NUnit.Framework;
    using Testing;

    public class When_receiving_a_raw_json_message : NServiceBusAcceptanceTest
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
                        var transport = cfg.GetConfiguredTransport();
                        transport.MessageUnwrapper = message => MyCustomUnwrapper(message, context.TestRunId);
                    });

                    b.When((bus, c) =>
                    {
                        var message = new MyMessage
                        {
                            SomeProperty = "Test"
                        };
                        var jsonPayload = JsonConvert.SerializeObject(message, new JsonSerializerSettings
                        {
                            TypeNameHandling = TypeNameHandling.All //we need this in order for $type="x" to be embedded in the json
                        });

                        var connectionString = Utilities.GetEnvConfiguredConnectionString();
                        var queueClient = new QueueClient(connectionString, "receivingarawjsonmessage-receiver");

                        return queueClient.SendMessageAsync(Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonPayload)));
                    });
                })
                .Done(c => c.GotMessage)
                .Run().ConfigureAwait(false);

            Assert.True(ctx.GotMessage);
            Assert.AreEqual("Test", ctx.MessageReceived.SomeProperty);
        }

        class Context : ScenarioContext
        {
            public bool GotMessage { get; set; }
            public MyMessage MessageReceived { get; set; }
        }

        class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServer>();
            }
        }

        static MessageWrapper MyCustomUnwrapper(QueueMessage rawMessage, Guid contextTestRunId)
        {
            var bytes = Convert.FromBase64String(rawMessage.MessageText);
            using (var stream = new MemoryStream(bytes))
            using (var streamReader = new StreamReader(stream))
            using (var textReader = new JsonTextReader(streamReader))
            {
                var wrapper = jsonSerializer.Deserialize<MessageWrapper>(textReader);

                if (!string.IsNullOrEmpty(wrapper.Id))
                {
                    return wrapper;
                }

                return new MessageWrapper
                {
                    Id = rawMessage.MessageId,
                    Headers = new Dictionary<string, string>
                    {
                        {TestIndependence.HeaderName, contextTestRunId.ToString()}
                    },
                    Body = bytes
                };
            }
        }

        static JsonSerializer jsonSerializer = JsonSerializer.Create();

        public class MyMessage : IMessage
        {
            public string SomeProperty { get; set; }
        }

        class MyMessageHandler : IHandleMessages<MyMessage>
        {
            public MyMessageHandler(Context ctx)
            {
                this.ctx = ctx;
            }

            public Task Handle(MyMessage message, IMessageHandlerContext context)
            {
                ctx.MessageReceived = message;
                ctx.GotMessage = true;

                return Task.FromResult(0);
            }

            Context ctx;
        }
    }
}