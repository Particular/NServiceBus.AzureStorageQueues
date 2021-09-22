namespace NServiceBus.Transport.AzureStorageQueues
{
    using System.Threading;
    using System.Threading.Tasks;

    interface INativeDelayDelivery
    {
        Task Start();
        Task Stop();
        Task<bool> ShouldDispatch(UnicastTransportOperation operation, CancellationToken cancellationToken);
    }
}