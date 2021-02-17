namespace NServiceBus.Transport.AzureStorageQueues.AcceptanceTests
{
    using Configuration.AdvancedExtensibility;

    static class EndpointConfigurationExtensions
    {
        public static string GetEndpointDefinedErrorQueue(this EndpointConfiguration configuration)
        {
            //TODO this is kind of a hack because the acceptance testing framework doesn't give any access to the defined error queue.
            return configuration.GetSettings().GetOrDefault<string>(ErrorQueueSettings.SettingsKey);
        }
    }
}
