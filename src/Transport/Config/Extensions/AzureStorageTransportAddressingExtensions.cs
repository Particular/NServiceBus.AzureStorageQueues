namespace NServiceBus
{
    using System;
    using System.Collections.Generic;
    using Configuration.AdvanceExtensibility;

    public static class AzureStorageTransportAddressingExtensions
    {
        public static TransportExtensions<AzureStorageQueueTransport> UseAccountNamesInsteadOfConnectionStrings(this TransportExtensions<AzureStorageQueueTransport> config,
            string defaultConnectionStringName,
            Dictionary<string, string> name2connectionString = null)
        {
            AzureStorageAddressingSettings settings;
            var settingsHolder = config.GetSettings();
            if (settingsHolder.TryGet(out settings))
            {
                throw new Exception("Safe connection strings has already been configured");
            }

            settings = new AzureStorageAddressingSettings();
            settings.UseAccountNamesInsteadOfConnectionStrings(defaultConnectionStringName, name2connectionString);
            settingsHolder.Set<AzureStorageAddressingSettings>(settings);

            return config;
        }
    }
}