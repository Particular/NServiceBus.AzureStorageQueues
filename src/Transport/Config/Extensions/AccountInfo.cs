namespace NServiceBus
{
    using System;
    using System.Collections.Generic;
    using global::Azure.Storage.Queues;

    /// <summary>
    /// An account info instance unifies an alias with a connection string and potentially registered endpoint instances.
    /// </summary>
    public class AccountInfo
    {
        /// <summary>
        /// Creates a new instance of an AccountInfo.
        /// </summary>
        /// <remarks>Prefer to use the overload that accepts a <see cref="QueueServiceClient"/>.</remarks>
        public AccountInfo(string alias, string connectionString) : this(alias, new QueueServiceClient(connectionString))
        {
            ConnectionString = connectionString;
        }

        /// <summary>
        /// Creates a new instance of an AccountInfo.
        /// </summary>
        public AccountInfo(string alias, QueueServiceClient queueServiceClient)
        {
            Guard.AgainstNull(nameof(alias), alias);
            Guard.AgainstNull(nameof(queueServiceClient), queueServiceClient);

            Alias = alias;
            QueueServiceClient = queueServiceClient;
            RegisteredEndpoints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The alias provided for the connection string represented by this account info instance.
        /// </summary>
        public string Alias { get; }

        /// <summary>
        /// The connection string
        /// </summary>
        /// <remarks>This property is only set when account info is constructed using the connection string.</remarks>
        public string ConnectionString { get; }

        /// <summary>
        /// The endpoints that belong to this account info instance.
        /// </summary>
        public HashSet<string> RegisteredEndpoints { get; }

        /// <summary>
        /// <see cref="QueueServiceClient"/> associated with the account.
        /// </summary>
        internal QueueServiceClient QueueServiceClient { get; }
    }
}