namespace NServiceBus
{
    /// <summary>
    /// Provides methods to define routing between Azure Storage accounts and map them to a logical name instead of using bare
    /// connection strings.
    /// </summary>
    public class AccountRoutingSettings
    {
        internal AccountRoutingSettings(AccountConfigurations accounts)
        {
            this.accounts = accounts;
        }

        /// <summary>
        /// Adds the mapping between the <paramref name="name" /> and its <paramref name="connectionString" />.
        /// </summary>
        public void AddAccount(string name, string connectionString)
        {
            accounts.Add(name, connectionString);
        }

        readonly AccountConfigurations accounts;
    }
}