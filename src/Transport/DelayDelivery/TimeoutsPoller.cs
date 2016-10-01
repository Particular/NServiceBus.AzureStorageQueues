namespace NServiceBus.Azure.Transports.WindowsAzureStorageQueues.DelayDelivery
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using AzureStorageQueues;
    using Logging;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Table;

    class TimeoutsPoller
    {
        const int TimeoutProcessedAtOnce = 100;
        static readonly TimeSpan NextRetrievalPollSleep = TimeSpan.FromMilliseconds(1000);
        static readonly TimeSpan LeaseLength = TimeSpan.FromSeconds(15);
        static ILog Logger = LogManager.GetLogger<TimeoutsPoller>();

        readonly string connectionString;
        readonly Dispatcher dispatcher;
        readonly string tableName;
        CloudTable table;
        LockManager lockManager;
        Task timeoutPollerTask;

        public TimeoutsPoller(string connectionString, Dispatcher dispatcher, string tableName)
        {
            this.connectionString = connectionString;
            this.dispatcher = dispatcher;
            this.tableName = tableName;
        }

        public void Start(CancellationToken token)
        {
            timeoutPollerTask = Task.Run(() => Poll(token));
        }

        public Task Stop()
        {
            return timeoutPollerTask;
        }

        async Task Poll(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await InnerPoll(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // ok, since the InnerPoll could observe the token
                }
                catch (Exception ex)
                {
                    Logger.Warn("Failed to fetch timeouts from the timeout storage", ex);
                }
            }

            try
            {
                await lockManager.TryRelease().ConfigureAwait(false);
            }
            catch
            {
                // ignored as lease will expire on its own
            }
        }

        async Task InnerPoll(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (await TryLease().ConfigureAwait(false))
                {
                    await SpinOnce(cancellationToken).ConfigureAwait(false);
                }

                await Task.Delay(NextRetrievalPollSleep, cancellationToken).ConfigureAwait(false);
            }
        }

        Task<bool> TryLease()
        {
            return lockManager.TryLockOrRenew();
        }

        async Task SpinOnce(CancellationToken cancellationToken)
        {
            var now = DateTimeOffset.UtcNow;
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            Logger.DebugFormat("Polling for timeouts at {0}.", now);

            var query = new TableQuery<TimeoutEntity>
            {
                FilterString = $"(PartitionKey le '{TimeoutEntity.GetPartitionKey(now)}') and (RowKey le '{TimeoutEntity.GetRawRowKeyPrefix(now)}'",
                TakeCount = TimeoutProcessedAtOnce // max batch size
            };

            var timeouts = await table.ExecuteQuerySegmentedAsync(query, null, cancellationToken).ConfigureAwait(false);

            var batch = new TableBatchOperation();
            foreach (var timeout in timeouts)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                // TODO: exceptions
                await Send(timeout).ConfigureAwait(false);
                batch.Delete(timeout);
                
            }

            // TODO: exceptions
            if (await TryLease().ConfigureAwait(false))
            {
                await table.ExecuteBatchAsync(batch).ConfigureAwait(false);
            }
            
            if (timeouts.Results.Count < TimeoutProcessedAtOnce)
            {
                await Task.Delay(NextRetrievalPollSleep, cancellationToken).ConfigureAwait(false);
            }
        }

        Task Send(TimeoutEntity timeout)
        {
            return dispatcher.Send(timeout.GetOperation());
        }

        public async Task Init()
        {
            var account = CloudStorageAccount.Parse(connectionString);
            table = await TimeoutEntity.BuiltTimeoutTableWithExplicitName(connectionString, tableName).ConfigureAwait(false);
            var container = account.CreateCloudBlobClient().GetContainerReference(table.Name.ToLower()); // TODO: can it be lowered?
            lockManager = new LockManager(container, LeaseLength);
        }
    }
}