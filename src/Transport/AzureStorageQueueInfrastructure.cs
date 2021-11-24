namespace NServiceBus.Transport.AzureStorageQueues
{
    using System.Collections.ObjectModel;
    using System.Threading;
    using System.Threading.Tasks;
    using Transport;

    class AzureStorageQueueInfrastructure : TransportInfrastructure
    {
        public AzureStorageQueueInfrastructure(AzureStorageQueueTransport transport, Dispatcher dispatcher, ReadOnlyDictionary<string, IMessageReceiver> receivers, NativeDelayedDeliveryProcessor nativeDelayedDeliveryProcessor)
        {
            Dispatcher = dispatcher;
            Receivers = receivers;
            this.transport = transport;
            this.nativeDelayedDeliveryProcessor = nativeDelayedDeliveryProcessor;
        }

        public override Task Shutdown(CancellationToken cancellationToken = default)
        {
            return nativeDelayedDeliveryProcessor.Stop(cancellationToken);
        }

#pragma warning disable CS0618 // Type or member is obsolete
        public override string ToTransportAddress(Transport.QueueAddress address) => transport.ToTransportAddress(address);
#pragma warning restore CS0618 // Type or member is obsolete

        readonly AzureStorageQueueTransport transport;
        readonly NativeDelayedDeliveryProcessor nativeDelayedDeliveryProcessor;
    }
}
