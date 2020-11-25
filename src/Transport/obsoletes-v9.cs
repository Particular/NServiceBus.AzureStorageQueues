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
}
#pragma warning restore 1591
