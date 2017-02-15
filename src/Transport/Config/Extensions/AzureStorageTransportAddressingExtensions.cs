namespace NServiceBus
{
    using AzureStorageQueues.Config;
    using Configuration.AdvanceExtensibility;

    public static class AzureStorageTransportAddressingExtensions
    {
        public static TransportExtensions<AzureStorageQueueTransport> UseAccountAliasesInsteadOfConnectionStrings(this TransportExtensions<AzureStorageQueueTransport> config)
        {
            Guard.AgainstNull(nameof(config), config);
            config.GetSettings().Set(WellKnownConfigurationKeys.UseAccountNamesInsteadOfConnectionStrings, true);
            return config;
        }

        /// <summary>
        /// Provides access to configure cross account routing.
        /// </summary>
        public static AccountRoutingSettings AccountRouting(this TransportExtensions<AzureStorageQueueTransport> transportExtensions)
        {
            Guard.AgainstNull(nameof(transportExtensions), transportExtensions);
            return new AccountRoutingSettings(transportExtensions.EnsureAccounts());
        }

        public static TransportExtensions<AzureStorageQueueTransport> DefaultAccountAlias(this TransportExtensions<AzureStorageQueueTransport> transportExtensions, string alias)
        {
            transportExtensions.EnsureAccounts().MapLocalAccount(alias);
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