namespace NServiceBus
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using global::Azure.Storage.Queues;
    using Microsoft.Azure.Cosmos.Table;

    /// <summary>
    /// An account info instance unifies an alias with a connection string and potentially registered endpoint instances.
    /// </summary>
    public class AccountInfo
    {
        /// <summary>
        /// Creates a new instance of an AccountInfo.
        /// </summary>
        /// <remarks>Prefer to use the overload that accepts a <see cref="QueueServiceClient"/>.</remarks>
        public AccountInfo(string alias, string connectionString) : this(alias, new QueueServiceClient(connectionString), CloudStorageAccount.Parse(connectionString).CreateCloudTableClient())
        {
            ConnectionString = connectionString;
        }

        /// <summary>
        /// Creates a new instance of an AccountInfo.
        /// </summary>
        public AccountInfo(string alias, QueueServiceClient queueServiceClient, CloudTableClient cloudTableClient)
        {
            Guard.AgainstNull(nameof(alias), alias);
            Guard.AgainstNull(nameof(queueServiceClient), queueServiceClient);
            Guard.AgainstNull(nameof(cloudTableClient), cloudTableClient);

            Alias = alias;
            QueueServiceClient = queueServiceClient;
            CloudTableClient = cloudTableClient;
            RegisteredEndpoints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            PublishedEventsByEndpoint = new Dictionary<string, (IEnumerable<Type> publishedEvents, string subscriptionTableName)>();
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="endpointName"></param>
        /// <param name="publishedEvents"></param>
        /// <param name="subscriptionTableName"></param>
        /// <returns></returns>
        public AccountInfo AddEndpoint(string endpointName, IEnumerable<Type> publishedEvents = null, string subscriptionTableName = null)
        {
            var tableName = string.IsNullOrEmpty(subscriptionTableName) ? SubscriptionSettings.DefaultSubscriptionTableName : subscriptionTableName;
            PublishedEventsByEndpoint.Add(endpointName, (publishedEvents ?? Enumerable.Empty<Type>(), tableName));

            return this;
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
        [ObsoleteEx(TreatAsErrorFromVersion = "10", RemoveInVersion = "11", ReplacementTypeOrMember = "AddEndpoint(string endpointName, IEnumerable<Type> publishedEvents = null, string subscriptionTableName = null)")]
        public HashSet<string> RegisteredEndpoints { get; }

        /// <summary>
        /// <see cref="QueueServiceClient"/> associated with the account.
        /// </summary>
        internal QueueServiceClient QueueServiceClient { get; }

        /// <summary>
        /// <see cref="CloudTableClient"/> associated with the account.
        /// </summary>
        internal CloudTableClient CloudTableClient { get; }

        /// <summary>
        /// Store specific endpoint's information related to the events it might publish and the subscriptions table name.
        /// <remarks>The dictionary key is the endpoint's name.</remarks>
        /// </summary>
        internal Dictionary<string, (IEnumerable<Type> publishedEvents, string subscriptionTableName)> PublishedEventsByEndpoint { get; }
    }
}