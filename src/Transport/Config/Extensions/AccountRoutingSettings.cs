namespace NServiceBus
{
    using System;
    using System.Collections.Generic;
    using global::Azure.Storage.Queues;
    using Microsoft.Azure.Cosmos.Table;

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

        // TODO: adjust as needed
        [ObsoleteEx(TreatAsErrorFromVersion = "10", RemoveInVersion = "11", ReplacementTypeOrMember = "AddAccount(string alias, QueueServiceClient queueServiceClient, CloudTableClient cloudTableClient)")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public AccountInfo AddAccount(string alias, QueueServiceClient queueServiceClient)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Adds the mapping between the <paramref alias="alias" /> its <paramref alias="QueueServiceClient" /> and <paramref alias="CloudTableClient" />.
        /// </summary>
        public AccountInfo AddAccount(string alias, QueueServiceClient queueServiceClient, CloudTableClient cloudTableClient)
        {
            if (Mappings.TryGetValue(alias, out var accountInfo))
            {
                return accountInfo;
            }

            accountInfo = new AccountInfo(alias, queueServiceClient, cloudTableClient);
            Mappings.Add(alias, accountInfo);

            return accountInfo;
        }

        internal Dictionary<string, AccountInfo> Mappings = new Dictionary<string, AccountInfo>();
        string defaultAccountAlias = string.Empty;
    }
}
