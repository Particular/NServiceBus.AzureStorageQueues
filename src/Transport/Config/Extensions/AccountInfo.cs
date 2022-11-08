namespace NServiceBus
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using global::Azure.Data.Tables;
    using global::Azure.Storage.Queues;

    /// <summary>
    /// An account info instance unifies an alias with a connection string and potentially registered endpoint instances.
    /// </summary>
    public partial class AccountInfo
    {
        /// <summary>
        /// Creates a new instance of an AccountInfo.
        /// </summary>
        /// <remarks>Prefer to use the overload that accepts a <see cref="QueueServiceClient"/>.</remarks>
        public AccountInfo(string alias, string connectionString)
            : this(alias, new QueueServiceClient(connectionString), new TableServiceClient(connectionString)) =>
            ConnectionString = connectionString;

        /// <summary>
        /// Creates a new instance of an AccountInfo.
        /// </summary>
        public AccountInfo(string alias, QueueServiceClient queueServiceClient, TableServiceClient tableServiceClient)
        {
            Guard.AgainstNull(nameof(alias), alias);
            Guard.AgainstNull(nameof(queueServiceClient), queueServiceClient);
            Guard.AgainstNull(nameof(tableServiceClient), tableServiceClient);

            Alias = alias;
            QueueServiceClient = queueServiceClient;
            TableServiceClient = tableServiceClient;
            PublishedEventsByEndpoint = new Dictionary<string, (IEnumerable<Type> publishedEvents, string subscriptionTableName)>();
        }

        /// <summary>
        /// Adds an endpoint to this account info instance.
        /// </summary>
        /// <param name="endpointName">The name of the endpoint belonging to this account.</param>
        /// <param name="publishedEvents">If the endpoint is a publisher and the subscriber is interested in subscribing to events published in this account the events subscribed to need to be listed here.</param>
        /// <param name="subscriptionTableName">The subscription table name to be used in case the publisher configuration doesn't use the default table name.</param>
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
        /// <see cref="QueueServiceClient"/> associated with the account.
        /// </summary>
        internal QueueServiceClient QueueServiceClient { get; }

        /// <summary>
        /// <see cref="TableServiceClient"/> associated with the account.
        /// </summary>
        internal TableServiceClient TableServiceClient { get; }

        /// <summary>
        /// Store specific endpoint's information related to the events it might publish and the subscriptions table name.
        /// <remarks>The dictionary key is the endpoint's name.</remarks>
        /// </summary>
        internal Dictionary<string, (IEnumerable<Type> publishedEvents, string subscriptionTableName)> PublishedEventsByEndpoint { get; }
    }
}