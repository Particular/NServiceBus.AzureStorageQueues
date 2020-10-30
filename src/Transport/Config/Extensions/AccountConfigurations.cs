namespace NServiceBus
{
    using System;
    using System.Collections.Generic;
    using global::Azure.Storage.Queues;

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

        public AccountInfo Add(string alias, QueueServiceClient client)
        {
            if(!mappings.TryGetValue(alias, out var accountInfo))
            {
                accountInfo = new AccountInfo(alias, client);
                mappings.Add(alias, accountInfo);
            }
            return accountInfo;
        }

        public AccountInfo Add(string alias, string connectionStringValue) => Add(alias, new QueueServiceClient(connectionStringValue));

        internal Dictionary<string, AccountInfo> mappings = new Dictionary<string, AccountInfo>();
        internal string defaultAlias;
    }
}