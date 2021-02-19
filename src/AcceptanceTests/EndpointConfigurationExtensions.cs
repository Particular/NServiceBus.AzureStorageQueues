namespace NServiceBus.Transport.AzureStorageQueues.AcceptanceTests
{
    using Configuration.AdvancedExtensibility;

    static class EndpointConfigurationExtensions
    {
        public static string GetEndpointDefinedErrorQueue(this EndpointConfiguration configuration)
        {
            //this is kind of a hack because the acceptance testing framework doesn't give any access to the defined error queue.
            //there is no point in having this in the Core ATT framework since ASQ is the only transport that needs this.
            return configuration.GetSettings().GetOrDefault<string>(ErrorQueueSettings.SettingsKey);
        }
    }
}
