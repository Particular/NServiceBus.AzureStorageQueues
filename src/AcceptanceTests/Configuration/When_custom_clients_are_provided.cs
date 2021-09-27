namespace NServiceBus.Transport.AzureStorageQueues.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using AcceptanceTesting.Support;
    using global::Azure.Storage.Blobs;
    using global::Azure.Storage.Queues;
    using Microsoft.Azure.Cosmos.Table;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_custom_clients_are_provided : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_use_the_custom_client()
        {
            var runSettings = new RunSettings();
            runSettings.Set("DoNotSetConnectionString", true);

            var context = await Scenario.Define<Context>()
                .WithEndpoint<Sender>(c => c
                    .When(e => e.Send(new MyRequest())))
                .WithEndpoint<Receiver>()
                .Done(c => c.InvokedHandler)
                .Run(runSettings).ConfigureAwait(false);

            Assert.IsTrue(context.InvokedHandler);
        }

        class Context : ScenarioContext
        {
            public bool InvokedHandler { get; set; }
        }

        class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServer>(e =>
                {
                    var storageAccount = CloudStorageAccount.Parse(ConfigureEndpointAzureStorageQueueTransport.AnotherConnectionString);

                    var transport = e.ConfigureAsqTransport();
                    transport.UseQueueServiceClient(new QueueServiceClient(ConfigureEndpointAzureStorageQueueTransport.AnotherConnectionString));
                    transport.UseBlobServiceClient(new BlobServiceClient(ConfigureEndpointAzureStorageQueueTransport.AnotherConnectionString));
                    transport.UseCloudTableClient(new CloudTableClient(storageAccount.TableStorageUri, storageAccount.Credentials));
                    transport.Routing().RouteToEndpoint(typeof(MyRequest), Conventions.EndpointNamingConvention(typeof(Receiver)));
                });
            }
        }

        class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServer>(e =>
                {
                    e.ConfigureAsqTransport().ConnectionString(ConfigureEndpointAzureStorageQueueTransport.AnotherConnectionString);
                });
            }

            class MyRequestHandler : IHandleMessages<MyRequest>
            {
                public MyRequestHandler(Context scenarioContext)
                {
                    this.scenarioContext = scenarioContext;
                }

                public Task Handle(MyRequest message, IMessageHandlerContext context)
                {
                    scenarioContext.InvokedHandler = true;
                    return Task.FromResult(0);
                }

                Context scenarioContext;
            }
        }

        public class MyRequest : IMessage
        {
        }
    }
}