namespace NServiceBus
{
    using System;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Provides options to define settings for the transport subscription feature.
    /// </summary>
    public class SubscriptionSettings
    {
        internal SubscriptionSettings()
        {
        }

        /// <summary>
        /// Override the default table name used for storing subscriptions.
        /// </summary>
        /// <remarks>All endpoints in a given account need to agree on that name in order for them to be able to subscribe to and publish events.</remarks>
        public string SubscriptionTableName
        {
            get => subscriptionTableName ?? DefaultSubscriptionTableName;
            set
            {
                Guard.AgainstNullAndEmpty(nameof(SubscriptionTableName), value);

                if (subscriptionTableNameRegex.IsMatch(value) == false)
                {
                    throw new ArgumentException($"{nameof(SubscriptionTableName)} must match the following regular expression '{subscriptionTableNameRegex}'");
                }

                subscriptionTableName = value.ToLower();
            }
        }

        /// <summary>
        /// Cache subscriptions for a given <see cref="TimeSpan" />.
        /// </summary>
        public TimeSpan CacheInvalidationPeriod
        {
            get => cacheInvalidationPeriod;
            set
            {
                Guard.AgainstNegativeAndZero(nameof(CacheInvalidationPeriod), value);
                cacheInvalidationPeriod = value;
            }
        }

        /// <summary>
        ///     Do not cache subscriptions.
        /// </summary>
        public bool DisableCaching { get; set; } = false;


        /// <summary>
        ///     Default to 5 seconds caching. If a system is under load that prevent doing an extra roundtrip for each Publish
        ///     operation. If
        ///     a system is not under load, doing an extra roundtrip every 5 seconds is not a problem and 5 seconds is small enough
        ///     value that
        ///     people accepts as we always say that subscription operation is not instantaneous.
        /// </summary>
        TimeSpan cacheInvalidationPeriod = TimeSpan.FromSeconds(5);

        string subscriptionTableName;
        internal const string DefaultSubscriptionTableName = "subscriptions";
        static readonly Regex subscriptionTableNameRegex = new Regex(@"^[A-Za-z][A-Za-z0-9]{2,62}$", RegexOptions.Compiled);
    }
}