using System;
using System.Collections.Generic;

namespace NServiceBus
{
    using global::Azure.Storage.Queues;

    /// <summary>
    /// Provides methods to define routing between Azure Storage accounts and map them to a logical alias instead of using bare
    /// connection strings.
    /// </summary>
    public class AccountRoutingSettings
    {
        internal AccountRoutingSettings()
        {

        }

        /// <summary>
        /// Get or set default account alias.
        /// </summary>
        public string DefaultAccountAlias
        {
            get => _defaultAccountAlias;
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new ArgumentException("Should not be null or white space", nameof(DefaultAccountAlias));
                }
                _defaultAccountAlias = value;
            }
        }

        /// <summary>
        /// Adds the mapping between the <paramref alias="alias" /> and its <paramref alias="connectionString" />.
        /// </summary>
        /// <remarks>Prefer to use the overload that accepts a <see cref="QueueServiceClient"/>.</remarks>
        public void AddAccount(string alias, string connectionString) => AddAccount(alias, new QueueServiceClient(connectionString));

        /// <summary>
        /// Adds the mapping between the <paramref alias="alias" /> and its <paramref alias="QueueServiceClient" />.
        /// </summary>
        public void AddAccount(string alias, QueueServiceClient connectionClient)
        {
            if (mappings.TryGetValue(alias, out var accountInfo))
            {
                return;
            }

            accountInfo = new AccountInfo(alias, connectionClient);
            mappings.Add(alias, accountInfo);
        }

        internal Dictionary<string, AccountInfo> mappings = new Dictionary<string, AccountInfo>();
        private string _defaultAccountAlias;
    }
}