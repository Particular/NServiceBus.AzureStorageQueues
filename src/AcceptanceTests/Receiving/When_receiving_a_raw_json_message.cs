namespace NServiceBus.Azure.Transports.WindowsAzureStorageQueues.AcceptanceTests.Receiving
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Queue;
    using Newtonsoft.Json;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

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
                        cfg.UseSerialization<NServiceBus.JsonSerializer>();
                        cfg.ConfigureAsqTransport()
                            .UnwrapMessagesWith(message => MyCustomUnwrapper(message, context.TestRunId));
                    });

                    b.When((bus, c) =>
                    {
                        var message = new MyMessage
                        {
                            SomeProperty = "Test"
                        };
                        var jsonPayload = JsonConvert.SerializeObject(message, new JsonSerializerSettings
                        {
                            TypeNameHandling = TypeNameHandling.All //we need this in order fo $type="x" to be embedded in the json
                        });

                        var cloudQueueMessage = new CloudQueueMessage(jsonPayload);

                        var connectionString = Utils.GetEnvConfiguredConnectionString();
                        var storageAccount = CloudStorageAccount.Parse(connectionString);
                        var queueClient = storageAccount.CreateCloudQueueClient();
                        var queue = queueClient.GetQueueReference("receivingarawjsonmessage-receiver");

                        return queue.AddMessageAsync(cloudQueueMessage);
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

        static MessageWrapper MyCustomUnwrapper(CloudQueueMessage rawMessage, Guid contextTestRunId)
        {
            using (var stream = new MemoryStream(rawMessage.AsBytes))
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
                    Id = rawMessage.Id,
                    Headers = new Dictionary<string, string>
                    {
                        {TestIndependence.HeaderName, contextTestRunId.ToString()}
                    },
                    Body = rawMessage.AsBytes
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