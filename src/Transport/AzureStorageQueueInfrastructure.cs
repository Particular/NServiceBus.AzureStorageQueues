namespace NServiceBus.Transport.AzureStorageQueues
{
    using System;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Transport;

    [Janitor.SkipWeaving]
    class AzureStorageQueueInfrastructure : TransportInfrastructure, IDisposable
    {
        public AzureStorageQueueInfrastructure(AzureStorageQueueTransport transport, Dispatcher dispatcher, ReadOnlyDictionary<string, IMessageReceiver> receivers, NativeDelayedDeliveryProcessor nativeDelayedDeliveryProcessor)
        {
            Dispatcher = dispatcher;
            Receivers = receivers;
            this.transport = transport;
            this.nativeDelayedDeliveryProcessor = nativeDelayedDeliveryProcessor;
        }

        public override async Task Shutdown(CancellationToken cancellationToken = default)
        {
            try
            {
                await Task.WhenAll(Receivers.Values.Select(pump => pump.StopReceive(cancellationToken)))
                    .ConfigureAwait(false);
                await nativeDelayedDeliveryProcessor.Stop(cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                Dispose();
            }
        }

        public void Dispose()
        {
            foreach (var receiver in Receivers.Values.Cast<MessageReceiver>())
            {
                receiver.Dispose();
            }
        }

        public override string ToTransportAddress(Transport.QueueAddress address)
            => TranslateAddress(address, transport.QueueAddressGenerator);

        internal static string TranslateAddress(Transport.QueueAddress address, QueueAddressGenerator addressGenerator)
        {
            var queue = new StringBuilder(address.BaseAddress);

            if (address.Discriminator != null)
            {
                queue.Append($"-{address.Discriminator}");
            }

            if (address.Qualifier != null)
            {
                queue.Append($"-{address.Qualifier}");
            }

            return addressGenerator.GetQueueName(queue.ToString());
        }

        readonly AzureStorageQueueTransport transport;
        readonly NativeDelayedDeliveryProcessor nativeDelayedDeliveryProcessor;
    }
}