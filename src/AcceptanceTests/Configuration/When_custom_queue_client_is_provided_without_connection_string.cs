namespace NServiceBus.Transport.AzureStorageQueues.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using AcceptanceTesting.Support;
    using global::Azure.Storage.Queues;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_custom_queue_client_is_provided_without_connection_string : NServiceBusAcceptanceTest
    {
        [Test]
        public void Should_throw()
        {
            var runSettings = new RunSettings();
            runSettings.Set("DoNotSetConnectionString", true);

            Assert.ThrowsAsync<Exception>(async () =>
                await Scenario.Define<Context>()
                    .WithEndpoint<Sender>(c => c
                        .When(e => e.Send(new MyRequest())))
                    .WithEndpoint<Receiver>()
                    .Done(c => c.InvokedHandler)
                    .Run(runSettings).ConfigureAwait(false), "Either a connection string or a table client has to be provided in the configuration.");
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
                    var transport = e.ConfigureAsqTransport();
                    transport.UseQueueServiceClient(new QueueServiceClient(ConfigureEndpointAzureStorageQueueTransport.AnotherConnectionString));
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