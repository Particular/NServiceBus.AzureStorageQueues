namespace NServiceBus
{
    using System;
    using Configuration.AdvancedExtensibility;
    using Settings;

    /// <summary>Configures native delayed delivery.</summary>
    public partial class DelayedDeliverySettings : ExposeSettings
    {
        NativeDelayedDeliverySettings transportDelayedDelivery;

        internal DelayedDeliverySettings(NativeDelayedDeliverySettings transportDelayedDelivery) : base(new SettingsHolder())
        {
            this.transportDelayedDelivery = transportDelayedDelivery;
        }

        /// <summary>Override the default table name used for storing delayed messages.</summary>
        /// <param name="delayedMessagesTableName">New table name.</param>
        [ObsoleteEx(
            Message = "Configure the transport via the AzureStorageQueueTransport DelayedDelivery.DelayedDeliveryTableName property",
            TreatAsErrorFromVersion = "12.0",
            RemoveInVersion = "13.0")]
        public void UseTableName(string delayedMessagesTableName)
        {
            transportDelayedDelivery.DelayedDeliveryTableName = delayedMessagesTableName;
        }

        /// <summary>
        /// Disable delayed delivery.
        /// <remarks>
        /// Disabling delayed delivery reduces costs associated with polling Azure Storage service for delayed messages that need
        /// to be dispatched.
        /// Do not use this setting if your endpoint requires delayed messages, timeouts, or delayed retries.
        /// </remarks>
        /// </summary>
        [ObsoleteEx(
            Message = "Configure delayed delivery support via the AzureStorageQueueTransport constructor.",
            TreatAsErrorFromVersion = "11.0",
            RemoveInVersion = "12.0")]
        public void DisableDelayedDelivery()
        {
            throw new NotImplementedException();
        }
    }
}

#pragma warning restore CS0618 // Type or member is obsolete