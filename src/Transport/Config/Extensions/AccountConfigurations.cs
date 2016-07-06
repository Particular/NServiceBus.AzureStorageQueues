namespace NServiceBus
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Holds mappings for used accounts.
    /// </summary>
    class AccountConfigurations
    {
        public void MapLocalAccount(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Should not be null or white space", nameof(name));
            }

            defaultName = name;
        }

        public void Add(string name, string connectionStringValue)
        {
            mappings.Add(name, connectionStringValue);
        }

        internal Dictionary<string, string> mappings = new Dictionary<string, string>();
        internal string defaultName;
    }
}