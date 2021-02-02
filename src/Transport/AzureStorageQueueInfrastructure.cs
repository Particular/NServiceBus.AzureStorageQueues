namespace NServiceBus.Transport.AzureStorageQueues
{
    using System.Collections.ObjectModel;
    using System.Threading.Tasks;
    using Transport;

    class AzureStorageQueueInfrastructure : TransportInfrastructure
    {
        public AzureStorageQueueInfrastructure(Dispatcher dispatcher, ReadOnlyCollection<IMessageReceiver> receivers, NativeDelayedDeliveryProcessor nativeDelayedDeliveryProcessor)
        {
            Dispatcher = dispatcher;
            Receivers = receivers;
            this.nativeDelayedDeliveryProcessor = nativeDelayedDeliveryProcessor;
        }

        public override Task DisposeAsync()
        {
            return nativeDelayedDeliveryProcessor.Stop();
        }

        readonly NativeDelayedDeliveryProcessor nativeDelayedDeliveryProcessor;
    }
}
