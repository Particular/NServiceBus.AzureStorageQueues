namespace NServiceBus.Transport.AzureStorageQueues
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    interface INativeDelayDelivery
    {
        Task Start();
        Task Stop();
        Task ScheduleDelivery(UnicastTransportOperation operation, DateTimeOffset at, CancellationToken cancellationToken);
    }
}