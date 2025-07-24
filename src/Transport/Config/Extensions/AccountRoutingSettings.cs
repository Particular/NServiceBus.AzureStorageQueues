namespace NServiceBus
{
    using System;
    using System.Collections.Generic;
    using global::Azure.Data.Tables;
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
            get;
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new ArgumentException("Should not be null or white space", nameof(DefaultAccountAlias));
                }

                field = value;
            }
        } = string.Empty;

        /// <summary>
        /// Adds the mapping between the <paramref alias="alias" /> its <paramref alias="QueueServiceClient" /> and <paramref alias="CloudTableClient" />.
        /// </summary>
        public AccountInfo AddAccount(string alias, QueueServiceClient queueServiceClient, TableServiceClient tableServiceClient)
        {
            if (Mappings.TryGetValue(alias, out var accountInfo))
            {
                return accountInfo;
            }

            accountInfo = new AccountInfo(alias, queueServiceClient, tableServiceClient);
            Mappings.Add(alias, accountInfo);

            return accountInfo;
        }

        internal Dictionary<string, AccountInfo> Mappings = [];
    }
}
