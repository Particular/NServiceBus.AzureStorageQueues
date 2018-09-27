namespace NServiceBus.Transports.AzureStorageQueues
{
    using Features;

    class PreventRoutingMessagesToTimeoutManager : Feature
    {
        public PreventRoutingMessagesToTimeoutManager()
        {
            EnableByDefault();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Pipeline.Remove("RouteDeferredMessageToTimeoutManager");
        }
    }
}