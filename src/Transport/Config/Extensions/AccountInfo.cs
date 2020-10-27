using Azure.Storage.Queues;

namespace NServiceBus
{
    using System;
    using System.Collections.Generic;
    using Transport.AzureStorageQueues;

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

            QueueServiceClient = new QueueServiceClient(connectionString);
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
        /// The endpoints that belong to this account info instance.
        /// </summary>
        public HashSet<string> RegisteredEndpoints { get; }

        /// <summary>
        /// <see cref="QueueServiceClient"/> associated with the account.
        /// </summary>
        internal QueueServiceClient QueueServiceClient { get; }
    }
}