namespace NServiceBus.Transport.AzureStorageQueues.AcceptanceTests
{
    using NServiceBus.AcceptanceTests.EndpointTemplates;

    public class CustomizedServer : DefaultServer
    {
        public CustomizedServer(AzureStorageQueueTransport transport)
        {
            TransportConfiguration = new ConfigureEndpointAzureStorageQueueTransport(transport);
        }
    }
}
