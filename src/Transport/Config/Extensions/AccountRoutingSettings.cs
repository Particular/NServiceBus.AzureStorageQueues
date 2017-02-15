namespace NServiceBus
{
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
        public void AddAccount(string alias, string connectionString)
        {
            Guard.AgainstNullAndEmpty(nameof(alias), alias);
            Guard.AgainstNullAndEmpty(nameof(connectionString), connectionString);
            accounts.Add(alias, connectionString);
        }

        readonly AccountConfigurations accounts;
    }
}