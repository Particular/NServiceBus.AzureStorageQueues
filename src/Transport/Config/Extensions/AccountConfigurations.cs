namespace NServiceBus
{
    using System;
    using System.Collections.Generic;
    using global::Azure.Storage.Queues;
    using Microsoft.Azure.Cosmos.Table;

    /// <summary>
    /// Holds mappings for used accounts.
    /// </summary>
    class AccountConfigurations
    {
        public void MapLocalAccount(string alias)
        {
            if (string.IsNullOrWhiteSpace(alias))
            {
                throw new ArgumentException("Should not be null or white space", nameof(alias));
            }

            defaultAlias = alias;
        }

        public AccountInfo Add(string alias, QueueServiceClient queueServiceClient, CloudTableClient cloudTableClient)
        {
            if (!mappings.TryGetValue(alias, out var accountInfo))
            {
                accountInfo = new AccountInfo(alias, queueServiceClient, cloudTableClient);
                mappings.Add(alias, accountInfo);
            }
            return accountInfo;
        }

        public AccountInfo Add(string alias, string connectionStringValue) => Add(alias, new QueueServiceClient(connectionStringValue), CloudStorageAccount.Parse(connectionStringValue).CreateCloudTableClient());

        internal Dictionary<string, AccountInfo> mappings = new Dictionary<string, AccountInfo>();
        internal string defaultAlias;
    }
}