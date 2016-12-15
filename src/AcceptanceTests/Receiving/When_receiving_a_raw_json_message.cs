namespace NServiceBus.Azure.Transports.WindowsAzureStorageQueues.AcceptanceTests.Receiving
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using MessageInterfaces;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Queue;
    using Newtonsoft.Json;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;
    using Serialization;
    using Settings;

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
        }

        class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServer>(config =>
                {
                    config.UseSerialization<NServiceBus.JsonSerializer>();
                    config.UseTransport<AzureStorageQueueTransport>()
                        .SerializeMessageWrapperWith<NativeAwareSerializer>();
                });
            }
        }


        class NativeAwareSerializer : SerializationDefinition
        {
            public override Func<IMessageMapper, IMessageSerializer> Configure(ReadOnlySettings settings)
            {
                return mapper => new Serializer();
            }

            class Serializer : IMessageSerializer
            {
                public void Serialize(object message, Stream stream)
                {
                    using (var streamWriter = new StreamWriter(stream))
                    {
                        using (var jsonWriter = new JsonTextWriter(streamWriter))
                        {
                            jsonSerializer.Serialize(jsonWriter, message);
                            jsonWriter.Flush();
                        }
                    }
                }

                public object[] Deserialize(Stream stream, IList<Type> messageTypes = null)
                {

                    using (var streamReader = new StreamReader(stream))
                    using (var textReader = new JsonTextReader(streamReader))
                    {
                        var wrapper = jsonSerializer.Deserialize<MessageWrapper>(textReader);

                        if (!string.IsNullOrEmpty(wrapper.Id))
                        {
                            return new object[] { wrapper };
                        }
                        stream.Seek(0, SeekOrigin.Begin);
                        return ReadNative(stream);
                    }
                }

                static object[] ReadNative(Stream stream)
                {
                    var nativeWrapper = new MessageWrapper();

                    nativeWrapper.Id = Guid.NewGuid().ToString(); //todo: How to deal with this?
                    nativeWrapper.Headers = new Dictionary<string, string>();

                    nativeWrapper.Body = ReadStream(stream);

                    return new object[]
                    {
                        nativeWrapper
                    };
                }

                static byte[] ReadStream(Stream input)
                {
                    var buffer = new byte[16 * 1024];
                    using (var ms = new MemoryStream())
                    {
                        int read;
                        while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            ms.Write(buffer, 0, read);
                        }
                        return ms.ToArray();
                    }
                }

                public string ContentType { get; } = "application/json";

                JsonSerializer jsonSerializer = JsonSerializer.Create();
            }
        }

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
                ctx.GotMessage = true;

                return Task.FromResult(0);
            }

            Context ctx;
        }
    }
}