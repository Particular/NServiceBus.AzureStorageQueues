using System;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting.Support;
using NServiceBus.AcceptanceTests.Routing.MessageDrivenSubscriptions;
using NUnit.Framework;
using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

public class ConfigureEndpointAzureStorageQueueTransport : IConfigureEndpointTestExecution
{
    internal static string ConnectionString => Testing.Utilities.GetEnvConfiguredConnectionString();
    internal static string AnotherConnectionString => Testing.Utilities.GetEnvConfiguredConnectionString2();

    public Task Configure(string endpointName, EndpointConfiguration configuration, RunSettings settings, PublisherMetadata publisherMetadata)
    {
        var connectionString = ConnectionString;

        var transportConfig = configuration
            .UseTransport<AzureStorageQueueTransport>()
            .ConnectionString(connectionString)
            .MessageInvisibleTime(TimeSpan.FromSeconds(30));

        transportConfig.SanitizeQueueNamesWith(BackwardsCompatibleQueueNameSanitizerForTests.Sanitize);

        transportConfig.DelayedDelivery().DisableTimeoutManager();

        var routingConfig = transportConfig.Routing();

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
        transportConfig.SerializeMessageWrapperWith<TestIndependence.TestIdAppendingSerializationDefinition<NewtonsoftSerializer>>();

        return Task.FromResult(0);
    }
    public Task Cleanup()
    {
        return Task.FromResult(0);
    }
}