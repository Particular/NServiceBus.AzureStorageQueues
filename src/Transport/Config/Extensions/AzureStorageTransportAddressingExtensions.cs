namespace NServiceBus
{
    using AzureStorageQueues.Config;
    using Configuration.AdvanceExtensibility;

    public static class AzureStorageTransportAddressingExtensions
    {
        public static TransportExtensions<AzureStorageQueueTransport> UseAccountNamesInsteadOfConnectionStrings(this TransportExtensions<AzureStorageQueueTransport> config)
        {
            config.GetSettings().Set(WellKnownConfigurationKeys.UseAccountNamesInsteadOfConnectionStrings, true);
            return config;
        }

        /// <summary>
        /// Provides access to configure cross account routing.
        /// </summary>
        public static AccountRoutingSettings AccountRouting(this TransportExtensions<AzureStorageQueueTransport> transportExtensions)
        {
            return new AccountRoutingSettings(transportExtensions.EnsureAccounts());
        }

        public static TransportExtensions<AzureStorageQueueTransport> DefaultAccountName(this TransportExtensions<AzureStorageQueueTransport> transportExtensions, string name)
        {
            transportExtensions.EnsureAccounts().MapLocalAccount(name);
            return transportExtensions;
        }

        static AccountConfigurations EnsureAccounts(this ExposeSettings transportExtensions)
        {
            var settings = transportExtensions.GetSettings();
            AccountConfigurations accounts;
            if (settings.TryGet(out accounts))
            {
                return accounts;
            }

            accounts = new AccountConfigurations();
            settings.Set<AccountConfigurations>(accounts);
            return accounts;
        }
    }
}