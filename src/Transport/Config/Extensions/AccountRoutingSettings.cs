namespace NServiceBus
{
    using System;
    using global::Azure.Storage.Queues;
    using Microsoft.Azure.Cosmos.Table;

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
            return accounts.Add(alias, queueServiceClient, cloudTableClient);
        }

        readonly AccountConfigurations accounts;
    }
}
