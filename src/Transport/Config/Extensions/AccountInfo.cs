namespace NServiceBus
{
    using System;
    using System.Collections.Generic;
    using Transports.AzureStorageQueues;

    /// <summary>
    /// An account info instance unifies an alias with a connection string and potentially registered endpoint instances.
    /// </summary>
    public class AccountInfo
    {
        /// <summary>
        /// Creates a new instance of an AccountInfo.
        /// </summary>
        public AccountInfo(string alias, string connectionString)
        {
            Guard.AgainstNull(nameof(alias), alias);
            Guard.AgainstNullAndEmpty(nameof(connectionString), connectionString);

            Alias = alias;
            Connection = new ConnectionString(connectionString);
            RegisteredEndpoints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The alias provided for the connection string represented by this account info instance.
        /// </summary>
        public string Alias { get; }

        /// <summary>
        /// The connection string.
        /// </summary>
        public string ConnectionString => Connection.Value;

        /// <summary>
        /// The endpoints that belong to this account info instance.
        /// </summary>
        public HashSet<string> RegisteredEndpoints { get; }

        internal ConnectionString Connection { get; }
    }
}