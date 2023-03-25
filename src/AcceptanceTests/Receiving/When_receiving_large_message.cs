namespace NServiceBus.Transport.AzureStorageQueues.AcceptanceTests
{
    using System;
    using System.Text;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using global::Azure.Storage.Queues;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

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

                        string contentCloseToLimits = new string('x', 63*1024/2);

                        return queueClient.SendMessageAsync(Convert.ToBase64String(Encoding.UTF8.GetBytes(contentCloseToLimits)));
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
            public Receiver() => EndpointSetup<DefaultServer>();
        }

        public class MyMessage : IMessage
        {
            public string SomeProperty { get; set; }
        }
    }
}