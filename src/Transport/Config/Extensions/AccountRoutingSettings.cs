namespace NServiceBus
{
    using global::Azure.Storage.Queues;

    /// <summary>
    /// Provides methods to define routing between Azure Storage accounts and map them to a logical alias instead of using bare
    /// connection strings.
    /// </summary>
    public class AccountRoutingSettings
    {
        internal AccountRoutingSettings(AccountConfigurations accounts)
        {
            this.accounts = accounts;
        }

        /// <summary>
        /// Adds the mapping between the <paramref alias="alias" /> and its <paramref alias="connectionString" />.
        /// </summary>
        /// <remarks>Prefer to use the overload that accepts a <see cref="QueueServiceClient"/>.</remarks>
        public AccountInfo AddAccount(string alias, string connectionString)
        {
            return accounts.Add(alias, connectionString);
        }

        /// <summary>
        /// Adds the mapping between the <paramref alias="alias" /> and its <paramref alias="QueueServiceClient" />.
        /// </summary>
        public AccountInfo AddAccount(string alias, QueueServiceClient connectionClient)
        {
            return accounts.Add(alias, connectionClient);
        }

        readonly AccountConfigurations accounts;
    }
}