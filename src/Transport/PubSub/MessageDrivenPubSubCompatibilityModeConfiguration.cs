namespace NServiceBus
{
    using System;
    using Configuration.AdvancedExtensibility;
    using Particular.Obsoletes;

    /// <summary>
    /// Configuration extensions for Message-Driven Pub-Sub compatibility mode
    /// </summary>
    public static class MessageDrivenPubSubCompatibilityModeConfiguration
    {
        /// <summary>
        ///     Enables compatibility with endpoints running on message-driven pub-sub
        /// </summary>
        /// <param name="routingSettings">The transport to enable pub-sub compatibility on</param>
        [Obsolete("Hybrid pub/sub is deprecated and endpoints needs to migrate to native pub/sub. Will be treated as an error from version 15.0.0. Will be removed in version 16.0.0.", false)]
        [ObsoleteMetadata(Message = "Hybrid pub/sub is deprecated and endpoints needs to migrate to native pub/sub",
            TreatAsErrorFromVersion = "15.0.0",
            RemoveInVersion = "16.0.0")]
        public static SubscriptionMigrationModeSettings EnableMessageDrivenPubSubCompatibilityMode(
            this RoutingSettings routingSettings)
        {
            var settings = routingSettings.GetSettings();
            settings.Set("NServiceBus.Subscriptions.EnableMigrationMode", true);
            return new SubscriptionMigrationModeSettings(settings);
        }
    }
}
