namespace NServiceBus
{
    using System;
    using System.Collections.Generic;

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

        public AccountInfo Add(string alias, string connectionStringValue)
        {
            if(!mappings.TryGetValue(alias, out var accountInfo))
            {
                accountInfo = new AccountInfo(alias, connectionStringValue);
                mappings.Add(alias, accountInfo);
            }
            return accountInfo;
        }

        internal Dictionary<string, AccountInfo> mappings = new Dictionary<string, AccountInfo>();
        internal string defaultAlias;
    }
}