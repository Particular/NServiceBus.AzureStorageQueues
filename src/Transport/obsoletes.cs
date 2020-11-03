#pragma warning disable 1591

namespace NServiceBus
{
    using System;

    public static partial class AzureStorageTransportAddressingExtensions
    {
        [ObsoleteEx(
            Message = "Account aliases are used instead of connection strings by default",
            RemoveInVersion = "10.0.0",
            TreatAsErrorFromVersion = "9.0.0")]
        public static TransportExtensions<AzureStorageQueueTransport> UseAccountAliasesInsteadOfConnectionStrings(this TransportExtensions<AzureStorageQueueTransport> config)
        {
            return config;
        }
    }

    public static partial class AzureStorageTransportExtensions
    {
        /// <summary>
        /// Registers a queue name sanitizer to apply to queue names not compliant wth Azure Storage Queue naming rules.
        /// <remarks>By default no sanitization is performed.</remarks>
        /// </summary>
        [ObsoleteEx(
            Message = "Queue name sanitization should be considered when providing the endpoint logical name using `new EndpointConfiguration(<name>)` API.",
            RemoveInVersion = "10.0.0",
            TreatAsErrorFromVersion = "9.0.0")]
        public static TransportExtensions<AzureStorageQueueTransport> SanitizeQueueNamesWith(this TransportExtensions<AzureStorageQueueTransport> config, Func<string, string> queueNameSanitizer)
        {
            return config;
        }
    }
}

#pragma warning restore 1591