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

                        var connectionString = Environment.GetEnvironmentVariable("AzureStorageQueueTransport.ConnectionString");

                        var storageAccount = CloudStorageAccount.Parse(connectionString);
                        var queueClient = storageAccount.CreateCloudQueueClient();
                        var queue = queueClient.GetQueueReference("receivingarawjsonmessage-receiver-retries");

                        return queue.AddMessageAsync(cloudQueueMessage);
                    });
                })
                .Done(c => c.GotMessage)
                .Run();

            Assert.True(ctx.GotMessage);
            Assert.AreEqual("Test", ctx.MessageReceived.SomeProperty);
        }

        class Context : ScenarioContext
        {
            public bool GotMessage { get; set; }
            public MyMessage MessageReceived { get; set; }
            public IReadOnlyDictionary<string, string> HeadersReceived { get; set; }
        }

        class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServer>(config =>
                {
                    config.UseSerialization<NServiceBus.JsonSerializer>();
                    config.UseTransport<AzureStorageQueueTransport>()
                        .UnwrapMessagesWith(MyCustomUnwrapper);
                });
            }
        }



        static MessageWrapper MyCustomUnwrapper(CloudQueueMessage rawMessage)
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
                    Headers = new Dictionary<string, string>(),
                    Body = rawMessage.AsBytes
                };
            }
        }

        static JsonSerializer jsonSerializer = JsonSerializer.Create();


        class MyMessage : IMessage
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
                ctx.HeadersReceived = context.MessageHeaders;
                ctx.GotMessage = true;

                return Task.FromResult(0);
            }

            Context ctx;
        }
    }
}