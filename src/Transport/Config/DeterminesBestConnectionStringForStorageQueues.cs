namespace NServiceBus.Azure.Transports.WindowsAzureStorageQueues
{
    using System.Configuration;
    using NServiceBus.Config;
    using NServiceBus.Settings;

    public class DeterminesBestConnectionStringForStorageQueues
    {
        readonly string defaultConnectionString;
        readonly ReadOnlySettings settings;

        public DeterminesBestConnectionStringForStorageQueues(ReadOnlySettings settings, string defaultConnectionString)
        {
            this.settings = settings;
            this.defaultConnectionString = defaultConnectionString;
        }

        public string Determine()
        {
            var configSection = settings.GetConfigSection<AzureQueueConfig>();
            var connectionString = configSection != null ? configSection.ConnectionString : string.Empty;

            if (string.IsNullOrEmpty(connectionString))
            {
                connectionString = defaultConnectionString;
            }

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new ConfigurationErrorsException("No Azure Storage Connection information specified, please set the ConnectionString");
            }

            return connectionString;
        }

        public bool IsPotentialStorageQueueConnectionString(string potentialConnectionString)
        {
            return potentialConnectionString.StartsWith("UseDevelopmentStorage=true") ||
                   potentialConnectionString.StartsWith("DefaultEndpointsProtocol=https");
        }

        public string Determine(string replyToAddress)
        {
            var replyQueue = replyToAddress.Queue;
            var connectionString = replyToAddress.Machine;

            if (!IsPotentialStorageQueueConnectionString(connectionString))
            {
                connectionString = Determine();
            }

            return replyQueue + "@" + connectionString;
        }
    }
}