using System;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting.Support;
using NServiceBus.AcceptanceTests.Routing.MessageDrivenSubscriptions;
using NServiceBus.AcceptanceTests.ScenarioDescriptors;
using NUnit.Framework;
using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;
using NServiceBus.AcceptanceTests.Routing;
using NServiceBus.AcceptanceTests.Versioning;

public class ConfigureEndpointAzureStorageQueueTransport : IConfigureEndpointTestExecution
{
    internal static string ConnectionString => EnvironmentHelper.GetEnvironmentVariable($"{nameof(AzureStorageQueueTransport)}_ConnectionString") ?? "UseDevelopmentStorage=true";

    public Task Configure(string endpointName, EndpointConfiguration configuration, RunSettings settings, PublisherMetadata publisherMetadata)
    {
        var connectionString = ConnectionString;

        var transportConfig = configuration
            .UseTransport<AzureStorageQueueTransport>()
            .ConnectionString(connectionString)
            .MessageInvisibleTime(TimeSpan.FromSeconds(30));

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

        // see https://github.com/Particular/NServiceBus/pull/4956
        if (endpointName.StartsWith(Conventions.EndpointNamingConvention(typeof(When_base_event_from_2_publishers.Publisher1))) ||
            endpointName.StartsWith(Conventions.EndpointNamingConvention(typeof(When_extending_command_routing.Receiver))) ||
            endpointName.StartsWith(Conventions.EndpointNamingConvention(typeof(When_publishing.Publisher))) ||
            endpointName.StartsWith(Conventions.EndpointNamingConvention(typeof(When_publishing.Publisher3))) ||
            endpointName.StartsWith(Conventions.EndpointNamingConvention(typeof(When_publishing_an_event_implementing_two_unrelated_interfaces.Publisher))) ||
            endpointName.StartsWith(Conventions.EndpointNamingConvention(typeof(When_publishing_an_interface.Publisher))) ||
            endpointName.StartsWith(Conventions.EndpointNamingConvention(typeof(When_publishing_an_interface_with_unobtrusive.Publisher))) ||
            endpointName.StartsWith(Conventions.EndpointNamingConvention(typeof(When_publishing_using_root_type.Publisher))) ||
            endpointName.StartsWith(Conventions.EndpointNamingConvention(typeof(When_multiple_versions_of_a_message_is_published.V2Publisher)))
            )
        {
            Assert.Ignore("Ignored until there is a case-insensitive way to discover a subscription.");
        }
 
        if (endpointName.StartsWith("RegisteringAdditionalDeserializers.CustomSerializationSender"))
        {
            Assert.Ignore("Ignored since this scenario is not supported by ASQ.");
        }

        configuration.UseSerialization<XmlSerializer>();

        configuration.Pipeline.Register("test-independence-skip", typeof(TestIndependence.SkipBehavior), "Skips messages from other runs");
        configuration.Pipeline.Register("test-independence-stamp", typeof(TestIndependence.StampOutgoingBehavior), "Stamps outgoing messages from this run");
        transportConfig.SerializeMessageWrapperWith<TestIndependence.TestIdAppendingSerializationDefinition<JsonSerializer>>();

        return Task.FromResult(0);
    }

    public Task Cleanup()
    {
        return Task.FromResult(0);
    }
}