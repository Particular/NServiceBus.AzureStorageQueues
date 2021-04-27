namespace NServiceBus.Transport.AzureStorageQueues
{
    using System.Collections.ObjectModel;
    using System.Threading;
    using System.Threading.Tasks;
    using Transport;

    class AzureStorageQueueInfrastructure : TransportInfrastructure
    {
        public AzureStorageQueueInfrastructure(Dispatcher dispatcher, ReadOnlyDictionary<string, IMessageReceiver> receivers, NativeDelayedDeliveryProcessor nativeDelayedDeliveryProcessor)
        {
            Dispatcher = dispatcher;
            Receivers = receivers;
            this.nativeDelayedDeliveryProcessor = nativeDelayedDeliveryProcessor;
        }

        public override Task Shutdown(CancellationToken cancellationToken = default)
        {
            return nativeDelayedDeliveryProcessor.Stop(cancellationToken);
        }

        readonly NativeDelayedDeliveryProcessor nativeDelayedDeliveryProcessor;
    }
}
