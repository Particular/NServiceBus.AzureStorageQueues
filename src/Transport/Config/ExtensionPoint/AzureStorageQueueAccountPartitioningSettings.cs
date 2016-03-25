namespace NServiceBus
{
    using System;
    using System.Collections.Generic;
    using NServiceBus.AzureStorageQueues;
    using NServiceBus.AzureStorageQueue.Addressing;
    using NServiceBus.Configuration.AdvanceExtensibility;
    using NServiceBus.Settings;

    public class AzureStorageQueueAccountPartitioningSettings : ExposeSettings
    {
        private readonly SettingsHolder _settings;

        public AzureStorageQueueAccountPartitioningSettings(SettingsHolder settings) 
            : base(settings)
        {
            _settings = settings;
        }

        public AzureStorageQueueAccountPartitioningSettings UseStrategy<T>()
            where T : IAccountPartitioningStrategy
        {
            _settings.Set(WellKnownConfigurationKeys.Addressing.Partitioning.Strategy, typeof(T));

            return this;
        }

        public AzureStorageQueueAccountPartitioningSettings AddStorageAccount(string name, string connectionString)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Storage account name can't be null or empty", nameof(name));

            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("Storage account connection string can't be null or empty", nameof(connectionString));

            Dictionary<string, string> accounts;
            if (!_settings.TryGet(WellKnownConfigurationKeys.Addressing.Partitioning.Accounts, out accounts))
            {
                accounts = new Dictionary<string, string>();
                _settings.Set(WellKnownConfigurationKeys.Addressing.Partitioning.Accounts, accounts);
            }

            if (!accounts.ContainsValue(connectionString))
                accounts[name] = connectionString;

            return this;
        }
    }
}