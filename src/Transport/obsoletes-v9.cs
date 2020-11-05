#pragma warning disable 1591

namespace NServiceBus
{
    using System;

    partial class DelayedDeliverySettings
    {
        [ObsoleteEx(
            Message = "The timeout manager has been removed in favor of native delayed delivery support provided by transports. See the upgrade guide for more details.",
            TreatAsErrorFromVersion = "10",
            RemoveInVersion = "11")]
        public void DisableTimeoutManager() => throw new NotImplementedException();
    }

}
#pragma warning restore 1591
