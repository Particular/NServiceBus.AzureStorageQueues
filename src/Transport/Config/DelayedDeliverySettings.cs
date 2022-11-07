namespace NServiceBus
{
    using Configuration.AdvancedExtensibility;
    using Settings;

    /// <summary>Configures native delayed delivery.</summary>
    public partial class DelayedDeliverySettings : ExposeSettings
    {
        readonly NativeDelayedDeliverySettings transportDelayedDelivery;

        internal DelayedDeliverySettings(NativeDelayedDeliverySettings transportDelayedDelivery) : base(new SettingsHolder())
            => this.transportDelayedDelivery = transportDelayedDelivery;

        /// <summary>Override the default table name used for storing delayed messages.</summary>
        /// <param name="delayedMessagesTableName">New table name.</param>
        [PreObsolete(
            Message = "Configure the transport via the AzureStorageQueueTransport DelayedDelivery.DelayedDeliveryTableName property",
            TreatAsErrorFromVersion = "12.0",
            RemoveInVersion = "13.0",
            Note = AzureStorageTransportExtensions.Note)]
        public void UseTableName(string delayedMessagesTableName)
            => transportDelayedDelivery.DelayedDeliveryTableName = delayedMessagesTableName;
    }
}