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

        public void Add(string alias, string connectionStringValue)
        {
            mappings.Add(alias, connectionStringValue);
        }

        internal Dictionary<string, string> mappings = new Dictionary<string, string>();
        internal string defaultAlias;
    }
}