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
            get => field ?? DefaultSubscriptionTableName;
            set
            {
                Guard.AgainstNullAndEmpty(nameof(SubscriptionTableName), value);

                if (subscriptionTableNameRegex.IsMatch(value) == false)
                {
                    throw new ArgumentException($"{nameof(SubscriptionTableName)} must match the following regular expression '{subscriptionTableNameRegex}'");
                }

                field = value.ToLower();
            }
        }

        /// <summary>
        /// Cache subscriptions for a given <see cref="TimeSpan" />.
        /// </summary>
        /// <remarks>
        /// Defaults to 5 seconds. This reduces the need for a roundtrip on every Publish operation, which is beneficial under load.
        /// When the system is not under load, performing a roundtrip every 5 seconds is acceptable.
        /// A 5-second cache duration strikes a good balance—short enough to be acceptable given that subscription propagation
        /// is not instantaneous.
        /// </remarks>
        public TimeSpan CacheInvalidationPeriod
        {
            get;
            set
            {
                Guard.AgainstNegativeAndZero(nameof(CacheInvalidationPeriod), value);
                field = value;
            }
        } = TimeSpan.FromSeconds(5);

        /// <summary>
        ///     Do not cache subscriptions.
        /// </summary>
        public bool DisableCaching { get; set; } = false;

        internal const string DefaultSubscriptionTableName = "subscriptions";
        static readonly Regex subscriptionTableNameRegex = new Regex(@"^[A-Za-z][A-Za-z0-9]{2,62}$", RegexOptions.Compiled);
    }
}