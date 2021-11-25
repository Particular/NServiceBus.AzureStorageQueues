namespace NServiceBus.Transport.AzureStorageQueues
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Transport;

    class AzureStorageQueueInfrastructure : TransportInfrastructure
    {
        public AzureStorageQueueInfrastructure(AzureStorageQueueTransport transport, Dispatcher dispatcher, NativeDelayedDeliveryProcessor nativeDelayedDeliveryProcessor)
        {
            Dispatcher = dispatcher;
            this.transport = transport;
            this.nativeDelayedDeliveryProcessor = nativeDelayedDeliveryProcessor;
        }

        public override Task Shutdown(CancellationToken cancellationToken = default)
        {
            return nativeDelayedDeliveryProcessor.Stop(cancellationToken);
        }

        public void BuildReceivers(ReceiveSettings[] receiveSettings, HostSettings hostSettings,
            MessageWrapperSerializer serializer, ISubscriptionStore subscriptionStore, IQueueServiceClientProvider queueServiceClientProvider, QueueAddressGenerator queueAddressGenerator)
        {
            var receivers = new Dictionary<string, IMessageReceiver>();

            foreach (var receiveSetting in receiveSettings)
            {
                var unwrapper = transport.MessageUnwrapper != null
                    ? (IMessageEnvelopeUnwrapper)new UserProvidedEnvelopeUnwrapper(transport.MessageUnwrapper)
                    : new DefaultMessageEnvelopeUnwrapper(serializer);

                var receiveAddress = ToTransportAddress(receiveSetting.ReceiveAddress);

                var subscriptionManager = new SubscriptionManager(subscriptionStore, hostSettings.Name, receiveAddress);

                var receiver = new AzureMessageQueueReceiver(unwrapper, queueServiceClientProvider, queueAddressGenerator, receiveSetting.PurgeOnStartup, transport.MessageInvisibleTime);

                receivers.Add(receiveSetting.Id,
                    new MessageReceiver(
                        receiveSetting.Id,
                        transport.TransportTransactionMode,
                        receiver,
                        subscriptionManager,
                        receiveAddress,
                        receiveSetting.ErrorQueue,
                        hostSettings.CriticalErrorAction,
                        transport.DegreeOfReceiveParallelism,
                        transport.ReceiverBatchSize,
                        transport.MaximumWaitTimeWhenIdle,
                        transport.PeekInterval));
            }

            Receivers = receivers;
        }

#pragma warning disable CS0618 // Type or member is obsolete
        public override string ToTransportAddress(Transport.QueueAddress address) => transport.ToTransportAddress(address);
#pragma warning restore CS0618 // Type or member is obsolete

        readonly AzureStorageQueueTransport transport;
        readonly NativeDelayedDeliveryProcessor nativeDelayedDeliveryProcessor;
    }
}
