namespace NServiceBus.Transport.AzureStorageQueues
{
    using System.Threading;
    using System.Threading.Tasks;
    using global::Azure.Storage.Blobs;
    using Logging;
    using Microsoft.Azure.Cosmos.Table;

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

        public void Start(CancellationToken cancellationToken = default)
        {
            if (!enabled)
            {
                return;
            }

            Logger.Debug("Starting delayed delivery poller");

            var isAtMostOnce = transportTransactionMode == TransportTransactionMode.None;
            poller = new DelayedMessagesPoller(
                delayedMessageStorageTable,
                blobServiceClient,
                errorQueueAddress,
                isAtMostOnce,
                dispatcher,
                backoffStrategy);

            // Start token is just passed through to the implementation which maintains its own token source for stopping
            poller.Start(cancellationToken);
        }

        public Task Stop(CancellationToken cancellationToken = default)
        {
            Logger.Debug("Stopping delayed delivery poller");
            return poller != null ? poller.Stop(cancellationToken) : Task.CompletedTask;
        }

        readonly Dispatcher dispatcher;
        CloudTable delayedMessageStorageTable;
        readonly BlobServiceClient blobServiceClient;
        readonly string errorQueueAddress;
        readonly TransportTransactionMode transportTransactionMode;
        readonly BackoffStrategy backoffStrategy;
        bool enabled;

        static readonly ILog Logger = LogManager.GetLogger<NativeDelayedDeliveryProcessor>();
        DelayedMessagesPoller poller;
    }
}