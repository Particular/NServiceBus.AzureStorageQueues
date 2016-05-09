namespace NServiceBus
{
    using System;
    using System.Collections.Generic;
    using Configuration.AdvanceExtensibility;

    public static class AzureStorageTransportAddressingExtensions
    {
        public static TransportExtensions<AzureStorageQueueTransport> UseAccountNamesInsteadOfConnectionStrings(this TransportExtensions<AzureStorageQueueTransport> config,
            string defaultConnectionStringName,
            Action<AccountMapping> map)
        {
            AzureStorageAddressingSettings settings;
            var settingsHolder = config.GetSettings();
            if (settingsHolder.TryGet(out settings))
            {
                throw new Exception("Safe connection strings has already been configured");
            }

            settings = new AzureStorageAddressingSettings();
            var mapping = new AccountMapping();
            map?.Invoke(mapping);
            settings.UseAccountNamesInsteadOfConnectionStrings(defaultConnectionStringName, mapping.mappings);
            settingsHolder.Set<AzureStorageAddressingSettings>(settings);

            return config;
        }
    }

    public sealed class AccountMapping
    {
        internal Dictionary<string,string> mappings = new Dictionary<string, string>();

        public void MapAccount(string name, string connectionStringValue)
        {
            mappings.Add(name, connectionStringValue);
        }
    }
}