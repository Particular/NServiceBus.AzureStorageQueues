namespace NServiceBus.Transport.AzureStorageQueues
{
    using System.Collections.ObjectModel;
    using System.Text;
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
            => nativeDelayedDeliveryProcessor.Stop(cancellationToken);

        public override string ToTransportAddress(Transport.QueueAddress address)
            => TranslateAddress(address, transport.QueueAddressGenerator);

        internal static string TranslateAddress(Transport.QueueAddress address, QueueAddressGenerator addressGenerator)
        {
            var queue = new StringBuilder(address.BaseAddress);

            if (address.Discriminator != null)
            {
                queue.Append("-" + address.Discriminator);
            }

            if (address.Qualifier != null)
            {
                queue.Append("-" + address.Qualifier);
            }

            return addressGenerator.GetQueueName(queue.ToString());
        }

        readonly AzureStorageQueueTransport transport;
        readonly NativeDelayedDeliveryProcessor nativeDelayedDeliveryProcessor;
    }
}