#pragma warning disable 1591

namespace NServiceBus
{
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
}

#pragma warning restore 1591