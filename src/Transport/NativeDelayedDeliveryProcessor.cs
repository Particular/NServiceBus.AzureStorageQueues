namespace NServiceBus
{
    using System.Threading;
    using System.Threading.Tasks;
    using global::Azure.Storage.Blobs;
    using Microsoft.Azure.Cosmos.Table;
    using Logging;
    using NServiceBus.Transport.AzureStorageQueues;

    class NativeDelayedDeliveryProcessor
    {
        public static NativeDelayedDeliveryProcessor Disabled()
        {
            return new NativeDelayedDeliveryProcessor();
        }

        NativeDelayedDeliveryProcessor()
        {
            enabled = false;
        }

        public NativeDelayedDeliveryProcessor(
            Dispatcher dispatcher,
            CloudTable delayedMessageStorageTable,
            BlobServiceClient blobServiceClient,
            string errorQueueAddress,
            TransportTransactionMode transportTransactionMode,
            BackoffStrategy backoffStrategy)
        {
            enabled = true;
            this.dispatcher = dispatcher;
            this.delayedMessageStorageTable = delayedMessageStorageTable;
            this.blobServiceClient = blobServiceClient;
            this.errorQueueAddress = errorQueueAddress;
            this.transportTransactionMode = transportTransactionMode;
            this.backoffStrategy = backoffStrategy;
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
                errorQueueAddress,
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

        readonly Dispatcher dispatcher;
        CloudTable delayedMessageStorageTable;
        readonly BlobServiceClient blobServiceClient;
        readonly string errorQueueAddress;
        readonly TransportTransactionMode transportTransactionMode;
        readonly BackoffStrategy backoffStrategy;
        bool enabled;
        CancellationTokenSource nativeDelayedMessagesCancellationSource;

        static readonly ILog Logger = LogManager.GetLogger<NativeDelayedDeliveryProcessor>();
        DelayedMessagesPoller poller;
    }
}