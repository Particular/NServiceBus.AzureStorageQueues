namespace NServiceBus.AcceptanceTests
{
    using AcceptanceTesting.Support;

    public partial class TestSuiteConstraints
    {
        public bool SupportsDtc => false;
        public bool SupportsCrossQueueTransactions => false;
        public bool SupportsNativePubSub => false;
        public bool SupportsDelayedDelivery => true;
        public bool SupportsOutbox => false;
        public bool SupportsPurgeOnStartup => false;
        public IConfigureEndpointTestExecution CreateTransportConfiguration() => new ConfigureEndpointAzureStorageQueueTransport();
        public IConfigureEndpointTestExecution CreatePersistenceConfiguration() => new ConfigureEndpointAcceptanceTestingPersistence();
    }
}
