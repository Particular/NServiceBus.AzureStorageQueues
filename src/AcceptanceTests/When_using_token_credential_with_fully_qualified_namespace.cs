namespace NServiceBus.Transport.AzureStorageQueues.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AzureStorageQueues;
    using global::Azure.Data.Tables;
    using global::Azure.Identity;
    using global::Azure.Storage.Blobs;
    using global::Azure.Storage.Queues;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;
    using Testing;

    public class When_using_token_credential_with_uri : NServiceBusAcceptanceTest
    {
        string baseUrlTemplate;

        [SetUp]
        public void Setup()
        {
            var connectionString = Utilities.GetEnvConfiguredConnectionString();
            var connectionStringSettings = ConnectionStringParser.ParseStringIntoSettings(connectionString, s => { });

            baseUrlTemplate = $"https://{connectionStringSettings[ConnectionStringParser.AccountNameSettingString]}.{{0}}.{connectionStringSettings[ConnectionStringParser.EndpointSuffixSettingString]}";
        }

        [Test]
        public async Task Should_work()
        {
            var context = await Scenario.Define<Context>()
                .WithEndpoint<Publisher>(b =>
                {
                    b.CustomConfig(c =>
                    {
                        var transport = CreateTransportWithDefaultAzureCredential();
                        c.UseTransport(transport);
                    });
                    b.When(session =>
                    {
                        var sendOptions = new SendOptions();
                        sendOptions.RouteToThisEndpoint();
                        sendOptions.DelayDeliveryWith(TimeSpan.FromMilliseconds(500));
                        return session.Send(new MyCommand(), sendOptions);
                    });
                })
                .WithEndpoint<Subscriber>(b =>
                {
                    b.CustomConfig(c =>
                    {
                        var transport = CreateTransportWithDefaultAzureCredential();
                        c.UseTransport(transport);
                    });
                })
                .Done(c => c.SubscriberGotEvent)
                .Run();

            Assert.That(context.SubscriberGotEvent, Is.True);
        }

        AzureStorageQueueTransport CreateTransportWithDefaultAzureCredential()
        {
            var defaultAzureCredential = new DefaultAzureCredential();
            var transport = new AzureStorageQueueTransport(
                new QueueServiceClient(new Uri(string.Format(baseUrlTemplate, "queue")), defaultAzureCredential),
                new BlobServiceClient(new Uri(string.Format(baseUrlTemplate, "blob")), defaultAzureCredential),
                new TableServiceClient(new Uri(string.Format(baseUrlTemplate, "table")), defaultAzureCredential)
            );
            return Utilities.SetTransportDefaultTestsConfiguration(transport);
        }

        public class Context : ScenarioContext
        {
            public bool SubscriberGotEvent { get; set; }
        }

        public class Publisher : EndpointConfigurationBuilder
        {
            public Publisher() => EndpointSetup<DefaultPublisher>();

            public class MyHandler : IHandleMessages<MyCommand>
            {
                public Task Handle(MyCommand message, IMessageHandlerContext context)
                    => context.Publish(new MyEvent());
            }
        }

        public class Subscriber : EndpointConfigurationBuilder
        {
            public Subscriber() => EndpointSetup<DefaultServer>();

            public class MyHandler(Context testContext) : IHandleMessages<MyEvent>
            {
                public Task Handle(MyEvent message, IMessageHandlerContext context)
                {
                    testContext.SubscriberGotEvent = true;
                    return Task.CompletedTask;
                }
            }
        }

        public class MyEvent : IEvent;

        public class MyCommand : ICommand;
    }
}