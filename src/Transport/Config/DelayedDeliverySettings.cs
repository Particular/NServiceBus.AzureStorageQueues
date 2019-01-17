namespace NServiceBus
{
    using System;
    using System.Text.RegularExpressions;
    using Configuration.AdvancedExtensibility;
    using Settings;
    using Transport.AzureStorageQueues;

    /// <summary>Configures native delayed delivery.</summary>
    public class DelayedDeliverySettings : ExposeSettings
    {
        internal DelayedDeliverySettings(SettingsHolder settings) : base(settings) { }

        /// <summary>Override the default table name used for storing delayed messages.</summary>
        /// <param name="delayedMessagesTableName">New table name.</param>
        public void UseTableName(string delayedMessagesTableName)
        {
            Guard.AgainstNullAndEmpty(nameof(delayedMessagesTableName), delayedMessagesTableName);

            if (tableNameRegex.IsMatch(delayedMessagesTableName) == false)
            {
                throw new ArgumentException($"{nameof(delayedMessagesTableName)} must match the following regular expression '{tableNameRegex}'");
            }

            this.GetSettings().Set(WellKnownConfigurationKeys.DelayedDelivery.TableName, delayedMessagesTableName.ToLower());
        }

        /// <summary>
        /// Disables the Timeout Manager for the endpoint. Before disabling ensure there all timeouts in the timeout store
        /// have been processed or migrated.
        /// </summary>
        public void DisableTimeoutManager()
        {
            this.GetSettings().Set(WellKnownConfigurationKeys.DelayedDelivery.EnableTimeoutManager, false);
        }

        /// <summary>
        /// Disable delayed delivery.
        /// <remarks>
        /// Disabling delayed delivery reduces costs associated with polling Azure Storage service for delayed messages that need
        /// to be dispatched.
        /// Do not use this setting if your endpoint required delayed messages, timeouts, or delayed retries.
        /// </remarks>
        /// </summary>
        public void DisableDelayedDelivery()
        {
            // disable delayed delivery
            this.GetSettings().Set(WellKnownConfigurationKeys.DelayedDelivery.DisableDelayedDelivery, true);

            // disable timeout manager
            this.GetSettings().Set(WellKnownConfigurationKeys.DelayedDelivery.EnableTimeoutManager, false);
        }

        static readonly Regex tableNameRegex = new Regex(@"^[A-Za-z][A-Za-z0-9]{2,62}$", RegexOptions.Compiled);
    }
}