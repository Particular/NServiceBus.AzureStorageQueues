namespace NServiceBus.AcceptanceTests
{
    using Configuration.AdvancedExtensibility;

    public static class CustomEndpointConfigurationExtensions
    {
        public static TransportExtensions<AzureStorageQueueTransport> ConfigureAsqTransport(this EndpointConfiguration configuration)
        {
            return new TransportExtensions<AzureStorageQueueTransport>(configuration.GetSettings());
        }
    }
}
