namespace NServiceBus
{
    using Configuration.AdvancedExtensibility;
    using Particular.Obsoletes;
    using Settings;

    /// <summary>Configures native delayed delivery.</summary>
    public class DelayedDeliverySettings : ExposeSettings
    {
        readonly NativeDelayedDeliverySettings transportDelayedDelivery;

        internal DelayedDeliverySettings(NativeDelayedDeliverySettings transportDelayedDelivery) : base(new SettingsHolder())
            => this.transportDelayedDelivery = transportDelayedDelivery;

        /// <summary>Override the default table name used for storing delayed messages.</summary>
        /// <param name="delayedMessagesTableName">New table name.</param>
        [PreObsolete("https://github.com/Particular/NServiceBus/issues/6811",
            ReplacementTypeOrMember = "AzureStorageQueueTransport.DelayedDelivery.DelayedDeliveryTableName",
            Note = "Should not be converted to an ObsoleteEx until API mismatch described in issue is resolved.")]
        public void UseTableName(string delayedMessagesTableName)
            => transportDelayedDelivery.DelayedDeliveryTableName = delayedMessagesTableName;
    }
}