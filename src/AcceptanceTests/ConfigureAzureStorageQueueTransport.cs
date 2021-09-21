using System;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting.Support;
using NUnit.Framework;

public class ConfigureEndpointAzureStorageQueueTransport : IConfigureEndpointTestExecution
{
    internal static string ConnectionString => Testing.Utilities.GetEnvConfiguredConnectionString();
    internal static string AnotherConnectionString => Testing.Utilities.GetEnvConfiguredConnectionString2();

    public Task Configure(string endpointName, EndpointConfiguration configuration, RunSettings settings, PublisherMetadata publisherMetadata)
    {
        var transport = configuration
            .UseTransport<AzureStorageQueueTransport>()
            .MessageInvisibleTime(TimeSpan.FromSeconds(30));

        if (!settings.TryGet("DoNotSetConnectionString", out bool ignoreConnectionString) || !ignoreConnectionString)
        {
            transport.ConnectionString(ConnectionString);
        }

        transport.SanitizeQueueNamesWith(BackwardsCompatibleQueueNameSanitizerForTests.Sanitize);

        transport.DelayedDelivery().DisableTimeoutManager();

        transport.DisableCaching();

        if (endpointName.StartsWith("RegisteringAdditionalDeserializers.CustomSerializationSender"))
        {
            Assert.Ignore("Ignored since this scenario is not supported by ASQ.");
        }

        configuration.UseSerialization<XmlSerializer>();

        configuration.Pipeline.Register("test-independence-skip", typeof(TestIndependence.SkipBehavior), "Skips messages from other runs");
        configuration.Pipeline.Register("test-independence-stamp", typeof(TestIndependence.StampOutgoingBehavior), "Stamps outgoing messages from this run");
        transport.SerializeMessageWrapperWith<TestIndependence.TestIdAppendingSerializationDefinition<NewtonsoftSerializer>>();

        return Task.FromResult(0);
    }
    public Task Cleanup() => Task.FromResult(0);
}