namespace NServiceBus.Azure.Transports.WindowsAzureStorageQueues.DelayDelivery
{
    using System;

    struct DispatchDecision
    {
        public readonly bool ShouldDispatch;
        public readonly TimeSpan? Delay;

        public DispatchDecision(bool shouldDispatch, TimeSpan? delay)
        {
            ShouldDispatch = shouldDispatch;
            Delay = delay;
        }
    }
}