namespace NServiceBus;

using System;

public partial class AzureStorageQueueTransport
{
    /// <inheritdoc />
    [ObsoleteEx(Message = "Inject the ITransportAddressResolver type to access the address translation mechanism at runtime. See the NServiceBus version 8 upgrade guide for further details.",
        TreatAsErrorFromVersion = "13",
        RemoveInVersion = "14")]
#pragma warning disable CS0672 // Member overrides obsolete member
    public override string ToTransportAddress(Transport.QueueAddress address)
        => throw new NotImplementedException();
#pragma warning restore CS0672 // Member overrides obsolete member

}
