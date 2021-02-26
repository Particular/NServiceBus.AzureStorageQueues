using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting.Support;
using NServiceBus.AcceptanceTests.Routing.MessageDrivenSubscriptions;
using NServiceBus.Transport.AzureStorageQueues.AcceptanceTests;
using NUnit.Framework;
using Testing;
using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

public class ConfigureEndpointAzureStorageQueueTransport : IConfigureEndpointTestExecution
{
    AzureStorageQueueTransport transport;

    public ConfigureEndpointAzureStorageQueueTransport(AzureStorageQueueTransport transport)
    {
        this.transport = transport;
    }

    public ConfigureEndpointAzureStorageQueueTransport()
    { }

    public Task Configure(string endpointName, EndpointConfiguration configuration, RunSettings settings, PublisherMetadata publisherMetadata)
    {
        if (transport == null)
        {
            var errorQueue = configuration.GetEndpointDefinedErrorQueue();
            var connectionString = Utilities.GetEnvConfiguredConnectionString();
            transport = Utilities.CreateTransportWithDefaultTestsConfiguration(connectionString, delayedDeliveryPoisonQueue: errorQueue);
        }

        var routingConfig = configuration.UseTransport(transport);

        foreach (var publisher in publisherMetadata.Publishers)
        {
            foreach (var eventType in publisher.Events)
            {
                routingConfig.RegisterPublisher(eventType, publisher.PublisherName);
            }
        }

        if (endpointName.StartsWith(Conventions.EndpointNamingConvention(typeof(When_unsubscribing_from_event.Publisher))))
        {
            Assert.Ignore("Ignored until issue #173 is resolved.");
        }

        if (endpointName.StartsWith("RegisteringAdditionalDeserializers.CustomSerializationSender"))
        {
            Assert.Ignore("Ignored since this scenario is not supported by ASQ.");
        }

        configuration.UseSerialization<XmlSerializer>();

        configuration.Pipeline.Register("test-independence-skip", typeof(TestIndependence.SkipBehavior), "Skips messages from other runs");
        configuration.Pipeline.Register("test-independence-stamp", typeof(TestIndependence.StampOutgoingBehavior), "Stamps outgoing messages from this run");

        return Task.CompletedTask;
    }

    public Task Cleanup()
    {
        return Task.CompletedTask;
    }
}