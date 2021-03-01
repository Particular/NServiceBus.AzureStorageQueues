namespace NServiceBus
{
    using System;
    using System.Collections.Generic;
    using global::Azure.Storage.Queues;

    /// <summary>
    /// Provides methods to define routing between Azure Storage accounts and map them to a logical alias instead of using bare
    /// connection strings.
    /// </summary>
    public partial class AccountRoutingSettings
    {
        internal AccountRoutingSettings()
        {

        }

        /// <summary>
        /// Get or set the default account alias.
        /// </summary>
        public string DefaultAccountAlias
        {
            get => defaultAccountAlias;
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new ArgumentException("Should not be null or white space", nameof(DefaultAccountAlias));
                }
                defaultAccountAlias = value;
            }
        }

        /// <summary>
        /// Adds the mapping between the <paramref alias="alias" /> and its <paramref alias="QueueServiceClient" />.
        /// </summary>
        public AccountInfo AddAccount(string alias, QueueServiceClient connectionClient)
        {
            if (Mappings.TryGetValue(alias, out var accountInfo))
            {
                return accountInfo;
            }

            accountInfo = new AccountInfo(alias, connectionClient);
            Mappings.Add(alias, accountInfo);

            return accountInfo;
        }

        internal Dictionary<string, AccountInfo> Mappings = new Dictionary<string, AccountInfo>();
        string defaultAccountAlias = string.Empty;
    }
}
