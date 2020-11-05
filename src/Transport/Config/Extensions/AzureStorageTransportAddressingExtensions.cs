namespace NServiceBus
{
    using Configuration.AdvancedExtensibility;

    /// <summary>Transport addressing extensions.</summary>
    public static partial class AzureStorageTransportAddressingExtensions
    {
        /// <summary>
        /// Provides access to configure cross account routing.
        /// </summary>
        public static AccountRoutingSettings AccountRouting(this TransportExtensions<AzureStorageQueueTransport> transportExtensions)
        {
            Guard.AgainstNull(nameof(transportExtensions), transportExtensions);
            var accountConfigurations = transportExtensions.GetSettings().GetOrCreate<AccountConfigurations>();
            return new AccountRoutingSettings(accountConfigurations);
        }

        /// <summary>
        /// Set default account alias.
        /// </summary>
        public static TransportExtensions<AzureStorageQueueTransport> DefaultAccountAlias(this TransportExtensions<AzureStorageQueueTransport> transportExtensions, string alias)
        {
            Guard.AgainstNull(nameof(transportExtensions), transportExtensions);
            var accountConfigurations = transportExtensions.GetSettings().GetOrCreate<AccountConfigurations>();
            accountConfigurations.MapLocalAccount(alias);
            return transportExtensions;
        }
    }
}