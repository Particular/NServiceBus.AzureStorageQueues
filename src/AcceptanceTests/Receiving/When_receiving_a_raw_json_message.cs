namespace NServiceBus.Azure.Transports.WindowsAzureStorageQueues.AcceptanceTests.Receiving
{
    using System;
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
                        var jsonPayload = JsonConvert.SerializeObject(new MyMessage(), Formatting.Indented, new JsonSerializerSettings
                        {
                            TypeNameHandling = TypeNameHandling.All
                        });

                        var message = new CloudQueueMessage(jsonPayload);

                        var connectionString = Environment.GetEnvironmentVariable("AzureStorageQueueTransport.ConnectionString");

                        var storageAccount = CloudStorageAccount.Parse(connectionString);
                        var queueClient = storageAccount.CreateCloudQueueClient();
                        var queue = queueClient.GetQueueReference("receivingarawjsonmessage-receiver-retries");

                        return queue.AddMessageAsync(message);
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
            public Receiver()
            {
                EndpointSetup<DefaultServer>();
            }
        }

        class MyMessage : IMessage
        {
        }

        class MyMessageHandler : IHandleMessages<MyMessage>
        {
            public MyMessageHandler(Context ctx)
            {
                this.ctx = ctx;
            }

            public Task Handle(MyMessage message, IMessageHandlerContext context)
            {
                ctx.GotMessage = true;

                return Task.FromResult(0);
            }

            Context ctx;
        }
    }
}