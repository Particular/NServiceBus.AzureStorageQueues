namespace NServiceBus.Transport.AzureStorageQueues.AcceptanceTests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AzureStorageQueues;
    using global::Azure.Core.Diagnostics;
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
        IDictionary<string, string> connectionStringSettings;
        string baseUrlTemplate;

        [SetUp]
        public void Setup()
        {
            var connectionString = Utilities.GetEnvConfiguredConnectionString();
            connectionStringSettings = ConnectionStringParser.ParseStringIntoSettings(connectionString, s => { });

            baseUrlTemplate = $"https://{connectionStringSettings[ConnectionStringParser.AccountNameSettingString]}.{{0}}.{connectionStringSettings[ConnectionStringParser.EndpointSuffixSettingString]}";
        }

        [Test]
        public async Task Should_work()
        {
            using var listener = AzureEventSourceListener.CreateConsoleLogger();

            var context = await Scenario.Define<Context>()
                .WithEndpoint<Publisher>(b =>
                {
                    b.CustomConfig(c =>
                    {
                        var defaultAzureCredential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
                        {
                            Diagnostics = { IsLoggingEnabled = true }
                        });
                        var transport = new AzureStorageQueueTransport(new QueueServiceClient(
                                new Uri(string.Format(baseUrlTemplate, "queue")),
                                defaultAzureCredential),
                            new BlobServiceClient(new Uri(string.Format(baseUrlTemplate, "blob")),
                                defaultAzureCredential),
                            new TableServiceClient(new Uri(string.Format(baseUrlTemplate, "table")),
                                defaultAzureCredential));
                        var transportWithDefaults = Utilities.SetTransportDefaultTestsConfiguration(transport);
                        c.UseTransport(transportWithDefaults);
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
                        var defaultAzureCredential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
                        {
                            Diagnostics = { IsLoggingEnabled = true }
                        });
                        var transport = new AzureStorageQueueTransport(new QueueServiceClient(
                                new Uri(
                                    $"https://{connectionStringSettings[ConnectionStringParser.AccountNameSettingString]}.queue.{connectionStringSettings[ConnectionStringParser.EndpointSuffixSettingString]}"),
                                defaultAzureCredential),
                            new BlobServiceClient(new Uri($"https://{connectionStringSettings[ConnectionStringParser.AccountNameSettingString]}.blob.{connectionStringSettings[ConnectionStringParser.EndpointSuffixSettingString]}"),
                                defaultAzureCredential),
                            new TableServiceClient(new Uri($"https://{connectionStringSettings[ConnectionStringParser.AccountNameSettingString]}.table.{connectionStringSettings[ConnectionStringParser.EndpointSuffixSettingString]}"),
                                defaultAzureCredential));
                        var transportWithDefaults = Utilities.SetTransportDefaultTestsConfiguration(transport);
                        c.UseTransport(transportWithDefaults);
                    });
                })
                .Done(c => c.SubscriberGotEvent)
                .Run();

            Assert.True(context.SubscriberGotEvent);
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