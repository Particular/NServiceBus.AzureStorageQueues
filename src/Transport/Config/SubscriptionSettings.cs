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
#if NET10_0_OR_GREATER
            get;
#else
            get => subscriptionTableName ?? DefaultSubscriptionTableName;
#endif

            set
            {
                Guard.AgainstNullAndEmpty(nameof(SubscriptionTableName), value);

                if (subscriptionTableNameRegex.IsMatch(value) == false)
                {
                    throw new ArgumentException($"{nameof(SubscriptionTableName)} must match the following regular expression '{subscriptionTableNameRegex}'");
                }
#if NET10_0_OR_GREATER
                field = value.ToLower();
#else
                subscriptionTableName = value.ToLower();
#endif
            }
#if NET10_0_OR_GREATER
        } = DefaultSubscriptionTableName;
#else
        }
#endif

        /// <summary>
        /// Cache subscriptions for a given <see cref="TimeSpan" />.
        /// Default to 5 seconds caching. If a system is under load that prevent doing an extra roundtrip for each Publish
        /// operation. If
        /// a system is not under load, doing an extra roundtrip every 5 seconds is not a problem and 5 seconds is small enough
        /// value that
        /// people accepts as we always say that subscription operation is not instantaneous.
        /// </summary>

        public TimeSpan CacheInvalidationPeriod
        {
#if NET10_0_OR_GREATER
            get;
#else
            get => cacheInvalidationPeriod;
#endif

            set
            {
                Guard.AgainstNegativeAndZero(nameof(CacheInvalidationPeriod), value);
#if NET10_0_OR_GREATER
                field = value;
#else
                cacheInvalidationPeriod = value;
#endif

            }
#if NET10_0_OR_GREATER
        } = TimeSpan.FromSeconds(5);
#else
        }
#endif

        /// <summary>
        ///     Do not cache subscriptions.
        /// </summary>
        public bool DisableCaching { get; set; } = false;


#if !NET10_0_OR_GREATER
        string subscriptionTableName;
        TimeSpan cacheInvalidationPeriod = TimeSpan.FromSeconds(5);
#endif

        internal const string DefaultSubscriptionTableName = "subscriptions";
        static readonly Regex subscriptionTableNameRegex = new Regex(@"^[A-Za-z][A-Za-z0-9]{2,62}$", RegexOptions.Compiled);
    }
}