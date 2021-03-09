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
        /// Override the default table name used for storing delayed messages.
        /// </summary>
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


        string subscriptionTableName;
        internal const string DefaultSubscriptionTableName = "subscriptions";
        static readonly Regex subscriptionTableNameRegex = new Regex(@"^[A-Za-z][A-Za-z0-9]{2,62}$", RegexOptions.Compiled);
    }
}