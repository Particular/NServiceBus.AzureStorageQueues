#pragma warning disable 1591

namespace NServiceBus
{
    using System;
    using System.Reflection;

    public static class MessageDrivenPubSubCompatibility
    {
        [ObsoleteEx(
            Message = @"Publisher registration has been moved to message-driven pub-sub migration mode.
var compatMode = transport.EnableMessageDrivenPubSubCompatibilityMode();
compatMode.RegisterPublisher(eventType, publisherEndpoint);",
            ReplacementTypeOrMember = "SubscriptionMigrationModeSettings.RegisterPublisher(routingSettings, eventType, publisherEndpoint)",
            TreatAsErrorFromVersion = "10.0",
            RemoveInVersion = "11.0")]
        public static void RegisterPublisher(this RoutingSettings<AzureStorageQueueTransport> routingSettings, Type eventType, string publisherEndpoint)
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(
            Message = @"Publisher registration has been moved to message-driven pub-sub migration mode.
var compatMode = transport.EnableMessageDrivenPubSubCompatibilityMode();
compatMode.RegisterPublisher(assembly, publisherEndpoint);",
            ReplacementTypeOrMember = "SubscriptionMigrationModeSettings.RegisterPublisher(routingSettings, assembly, publisherEndpoint)",
            TreatAsErrorFromVersion = "10.0",
            RemoveInVersion = "11.0")]
        public static void RegisterPublisher(this RoutingSettings<AzureStorageQueueTransport> routingSettings, Assembly assembly, string publisherEndpoint)
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(
            Message = @"Publisher registration has been moved to message-driven pub-sub migration mode.
var compatMode = transport.EnableMessageDrivenPubSubCompatibilityMode();
compatMode.RegisterPublisher(assembly, namespace, publisherEndpoint);",
            ReplacementTypeOrMember = "SubscriptionMigrationModeSettings.RegisterPublisher(routingSettings, assembly, namespace, publisherEndpoint)",
            TreatAsErrorFromVersion = "10.0",
            RemoveInVersion = "11.0")]
        public static void RegisterPublisher(this RoutingSettings<AzureStorageQueueTransport> routingSettings, Assembly assembly, string @namespace, string publisherEndpoint)
        {
            throw new NotImplementedException();
        }
    }
}

#pragma warning restore 1591