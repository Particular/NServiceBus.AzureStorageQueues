namespace NServiceBus.Transport.AzureStorageQueues.AcceptanceTests
{
    using NServiceBus.AcceptanceTesting.EndpointTemplates;
    using Testing;

    public class CustomizedServer : DefaultServer
    {
        public CustomizedServer(AzureStorageQueueTransport transport)
        {
            TransportConfiguration = new ConfigureEndpointAzureStorageQueueTransport(transport);
        }

        public CustomizedServer(bool supportsNativeDelayedDelivery = true, bool supportsPublishSubscribe = true)
        {
            var transport = new AzureStorageQueueTransport(Utilities.GetEnvConfiguredConnectionString(),
                supportsNativeDelayedDelivery, supportsPublishSubscribe);

            Utilities.SetTransportDefaultTestsConfiguration(transport);

            TransportConfiguration = new ConfigureEndpointAzureStorageQueueTransport(transport);
        }
    }
}
