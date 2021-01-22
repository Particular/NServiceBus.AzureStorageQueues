using System;
using System.Text.RegularExpressions;

namespace NServiceBus
{
    /// <summary>
    /// Provides options to define settings for the transport DelayedDelivery feature.
    /// </summary>
    public class NativeDelayedDeliverySettings
    {
        internal NativeDelayedDeliverySettings()
        {

        }

        /// <summary>
        /// The queue to send messages to when native delayed delivery dispatch fails.
        /// When running in the context of an NServiceBus endpoint, if not set, this
        /// value defaults to the endpoint error queue.
        /// </summary>
        public string DelayedDeliveryPoisonQueue { get; set; }

        /// <summary>
        /// Override the default table name used for storing delayed messages.
        /// </summary>
        public string DelayedDeliveryTableName
        {
            get => delayedDeliveryTableName;
            set
            {
                Guard.AgainstNullAndEmpty(nameof(DelayedDeliveryTableName), value);

                if (delayedDeliveryTableNameRegex.IsMatch(DelayedDeliveryTableName) == false)
                {
                    throw new ArgumentException($"{nameof(DelayedDeliveryTableName)} must match the following regular expression '{delayedDeliveryTableNameRegex}'");
                }

                delayedDeliveryTableName = value;
            }
        }

        private string delayedDeliveryTableName;
        static readonly Regex delayedDeliveryTableNameRegex = new Regex(@"^[A-Za-z][A-Za-z0-9]{2,62}$", RegexOptions.Compiled);
    }
}