using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Microsoft.Azure.Cosmos.Table;
using NServiceBus.Logging;
using NServiceBus.Transport.AzureStorageQueues;

namespace NServiceBus
{
    class NativeDelayedDeliveryProcessor
    {
        public static NativeDelayedDeliveryProcessor Disabled()
        {
            return new NativeDelayedDeliveryProcessor();
        }

        private NativeDelayedDeliveryProcessor()
        {
            enabled = false;
        }

        public NativeDelayedDeliveryProcessor(
            Dispatcher dispatcher,
            CloudTable delayedMessageStorageTable,
            BlobServiceClient blobServiceClient,
            ImmutableDictionary<string, string> errorQueueAddresses,
            TransportTransactionMode transportTransactionMode,
            BackoffStrategy backoffStrategy,
            string userDefinedDelayedDeliveryPoisonQueue)
        {
            enabled = true;
            this.dispatcher = dispatcher;
            this.delayedMessageStorageTable = delayedMessageStorageTable;
            this.blobServiceClient = blobServiceClient;
            this.errorQueueAddresses = errorQueueAddresses;
            this.transportTransactionMode = transportTransactionMode;
            this.backoffStrategy = backoffStrategy;
            this.userDefinedDelayedDeliveryPoisonQueue = userDefinedDelayedDeliveryPoisonQueue;
        }

        public void Start()
        {
            if (!enabled)
            {
                return;
            }

            Logger.Debug("Starting delayed delivery poller");

            nativeDelayedMessagesCancellationSource = new CancellationTokenSource();

            var isAtMostOnce = transportTransactionMode == TransportTransactionMode.None;
            poller = new DelayedMessagesPoller(
                delayedMessageStorageTable,
                blobServiceClient,
                errorQueueAddresses,
                isAtMostOnce,
                dispatcher,
                backoffStrategy);
            poller.Start(nativeDelayedMessagesCancellationSource.Token);
        }

        public Task Stop()
        {
            Logger.Debug("Stopping delayed delivery poller");
            nativeDelayedMessagesCancellationSource?.Cancel();
            return poller != null ? poller.Stop() : Task.CompletedTask;
        }

        private readonly Dispatcher dispatcher;
        private CloudTable delayedMessageStorageTable;
        private readonly BlobServiceClient blobServiceClient;
        private readonly ImmutableDictionary<string, string> errorQueueAddresses;
        private readonly TransportTransactionMode transportTransactionMode;
        private readonly BackoffStrategy backoffStrategy;
        private readonly string userDefinedDelayedDeliveryPoisonQueue;
        private bool enabled;
        private CancellationTokenSource nativeDelayedMessagesCancellationSource;

        static readonly ILog Logger = LogManager.GetLogger<NativeDelayedDeliveryProcessor>();
        private DelayedMessagesPoller poller;
    }
}