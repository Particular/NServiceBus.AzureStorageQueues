#pragma warning disable 1591

namespace NServiceBus
{
    using System;

    public static partial class AzureStorageTransportAddressingExtensions
    {
        [ObsoleteEx(
            Message = "Account aliases are used instead of connection strings by default",
            TreatAsErrorFromVersion = "9",
            RemoveInVersion = "10")]
        public static TransportExtensions<AzureStorageQueueTransport> UseAccountAliasesInsteadOfConnectionStrings(this TransportExtensions<AzureStorageQueueTransport> config) => throw new NotImplementedException();
    }

    public static partial class AzureStorageTransportExtensions
    {
        [ObsoleteEx(
            Message = "Queue name sanitization should be considered when providing the endpoint logical name using `new EndpointConfiguration(<name>)` API.",
            RemoveInVersion = "10.0.0",
            TreatAsErrorFromVersion = "9.0.0")]
        public static TransportExtensions<AzureStorageQueueTransport> SanitizeQueueNamesWith(this TransportExtensions<AzureStorageQueueTransport> config, Func<string, string> queueNameSanitizer) => throw new NotImplementedException();
    }
}
#pragma warning restore 1591
