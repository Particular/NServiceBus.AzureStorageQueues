namespace NServiceBus
{
    using Configuration.AdvancedExtensibility;
    using Transports.AzureStorageQueues;

    /// <summary>Transport addressing extensions.</summary>
    public static class AzureStorageTransportAddressingExtensions
    {
        /// <summary>
        /// Configure the transport to use account aliases instead of raw connection strings.
        /// </summary>
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

        /// <summary>
        /// Set default account alias.
        /// </summary>
        public static TransportExtensions<AzureStorageQueueTransport> DefaultAccountAlias(this TransportExtensions<AzureStorageQueueTransport> transportExtensions, string alias)
        {
            Guard.AgainstNull(nameof(transportExtensions), transportExtensions);
            transportExtensions.EnsureAccounts().MapLocalAccount(alias);
            return transportExtensions;
        }

        static AccountConfigurations EnsureAccounts(this ExposeSettings transportExtensions)
        {
            var settings = transportExtensions.GetSettings();
            if (settings.TryGet<AccountConfigurations>(out var accounts))
            {
                return accounts;
            }

            accounts = new AccountConfigurations();
            settings.Set<AccountConfigurations>(accounts);
            return accounts;
        }
    }
}