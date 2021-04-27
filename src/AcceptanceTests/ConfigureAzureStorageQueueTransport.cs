using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting.Support;
using NServiceBus.Transport.AzureStorageQueues.AcceptanceTests;
using NUnit.Framework;
using Testing;

public class ConfigureEndpointAzureStorageQueueTransport : IConfigureEndpointTestExecution
{
    AzureStorageQueueTransport transport;

    public ConfigureEndpointAzureStorageQueueTransport(AzureStorageQueueTransport transport)
    {
        this.transport = transport;
    }

    public ConfigureEndpointAzureStorageQueueTransport()
    { }

#pragma warning disable PS0018 // A task-returning method should have a CancellationToken parameter unless it has a parameter implementing ICancellableContext
    public Task Configure(string endpointName, EndpointConfiguration configuration, RunSettings settings, PublisherMetadata publisherMetadata)
#pragma warning restore PS0018 // A task-returning method should have a CancellationToken parameter unless it has a parameter implementing ICancellableContext
    {
        if (transport == null)
        {
            var errorQueue = configuration.GetEndpointDefinedErrorQueue();
            var connectionString = Utilities.GetEnvConfiguredConnectionString();
            transport = Utilities.CreateTransportWithDefaultTestsConfiguration(connectionString, delayedDeliveryPoisonQueue: errorQueue);
        }

        transport.Subscriptions.DisableCaching = true;

        var routingConfig = configuration.UseTransport(transport);

        foreach (var publisher in publisherMetadata.Publishers)
        {
            foreach (var eventType in publisher.Events)
            {
                routingConfig.RegisterPublisher(eventType, publisher.PublisherName);
            }
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

#pragma warning disable PS0018 // A task-returning method should have a CancellationToken parameter unless it has a parameter implementing ICancellableContext
    public Task Cleanup()
#pragma warning restore PS0018 // A task-returning method should have a CancellationToken parameter unless it has a parameter implementing ICancellableContext
    {
        return Task.CompletedTask;
    }
}