namespace NServiceBus.Azure.Transports.WindowsAzureStorageQueues.DelayDelivery
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using AzureStorageQueues;
    using ConsistencyGuarantees;
    using Logging;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Table;
    using Settings;
    using Transport;

    class TimeoutsPoller
    {
        const int TimeoutProcessedAtOnce = 50;
        static readonly TimeSpan LeaseLength = TimeSpan.FromSeconds(15);
        static readonly TimeSpan HalfOfLeaseLength = TimeSpan.FromTicks(LeaseLength.Ticks / 2);
        static ILog Logger = LogManager.GetLogger<TimeoutsPoller>();

        readonly string connectionString;
        readonly Dispatcher dispatcher;
        readonly string tableName;
        readonly BackoffStrategy backoffStrategy;

        CloudTable table;
        LockManager lockManager;
        Task timeoutPollerTask;
        string errorQueue;
        bool isAtMostOnce;

        public TimeoutsPoller(string connectionString, Dispatcher dispatcher, string tableName, BackoffStrategy backoffStrategy)
        {
            this.connectionString = connectionString;
            this.dispatcher = dispatcher;
            this.tableName = tableName;
            this.backoffStrategy = backoffStrategy;
        }

        public async Task Start(ReadOnlySettings settings, CancellationToken cancellationToken)
        {
            await Init(settings, cancellationToken).ConfigureAwait(false);
            // No need to pass token to run. to avoid when token is cancelled the task changing into 
            // the cancelled state and when awaited while stopping rethrow the cancelled exception
            timeoutPollerTask = Task.Run(() => Poll(cancellationToken));
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
                    await InnerPoll(cancellationToken)
                        .ConfigureAwait(false);
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
                await lockManager.TryRelease(cancellationToken)
                    .ConfigureAwait(false);
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
                if (await TryLease(cancellationToken)
                    .ConfigureAwait(false))
                {
                    try
                    {
                        await SpinOnce(cancellationToken)
                            .ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn("Failed at spinning the poller", ex);
                        await BackoffOnError(cancellationToken)
                            .ConfigureAwait(false);
                    }
                }
                else
                {
                    await BackoffOnError(cancellationToken)
                        .ConfigureAwait(false);
                }
            }
        }

        Task BackoffOnError(CancellationToken cancellationToken)
        {
            // run as there was no messages at all
            return backoffStrategy.OnBatch(TimeoutProcessedAtOnce, 0, cancellationToken);
        }

        Task<bool> TryLease(CancellationToken cancellationToken)
        {
            return lockManager.TryLockOrRenew(cancellationToken);
        }

        async Task SpinOnce(CancellationToken cancellationToken)
        {
            var now = NativeDelayDelivery.UtcNow;
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            Logger.DebugFormat("Polling for timeouts at {0}.", now);

            var query = new TableQuery<TimeoutEntity>
            {
                FilterString = $"(PartitionKey le '{TimeoutEntity.GetPartitionKey(now)}') and (RowKey le '{TimeoutEntity.GetRawRowKeyPrefix(now)}')",
                TakeCount = TimeoutProcessedAtOnce // max batch size
            };

            var timeouts = await table.ExecuteQueryAsync(query, TimeoutProcessedAtOnce, cancellationToken)
                .ConfigureAwait(false);

            if (await TryLease(cancellationToken).ConfigureAwait(false) == false)
            {
                return;
            }

            var stopwatch = Stopwatch.StartNew();

            foreach (var timeout in timeouts)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                // after half check if the lease is active
                if (stopwatch.Elapsed > HalfOfLeaseLength)
                {
                    if (await TryLease(cancellationToken).ConfigureAwait(false) == false)
                    {
                        return;
                    }
                    stopwatch.Reset();
                }

                try
                {
                    var delete = TableOperation.Delete(timeout);

                    if (isAtMostOnce)
                    {
                        // delete first, then dispatch
                        await table.ExecuteAsync(delete, cancellationToken).ConfigureAwait(false);
                        await SafeDispatch(timeout, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        // dispatch first, then delete
                        await SafeDispatch(timeout, cancellationToken).ConfigureAwait(false);
                        await table.ExecuteAsync(delete, cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (Exception exception)
                {
                    // just log and move on with the rest
                    Logger.Warn($"Failed at dispatching the timeout PK:'{timeout.PartitionKey}' RK: '{timeout.RowKey}' with message id '{timeout.MessageId}'", exception);
                }
            }

            await backoffStrategy.OnBatch(TimeoutProcessedAtOnce, timeouts.Count, cancellationToken).ConfigureAwait(false);
        }

        async Task SafeDispatch(TimeoutEntity timeout, CancellationToken cancellationToken)
        {
            var operation = timeout.GetOperation();
            try
            {
                await dispatcher.Send(operation, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // if send fails for any reason
                await dispatcher.Send(CreateOperationForErrorQueue(operation), cancellationToken).ConfigureAwait(false);
            }
        }

        UnicastTransportOperation CreateOperationForErrorQueue(UnicastTransportOperation operation)
        {
            return new UnicastTransportOperation(operation.Message, errorQueue, operation.RequiredDispatchConsistency, operation.DeliveryConstraints);
        }

        async Task Init(ReadOnlySettings settings, CancellationToken cancellationToken)
        {
            errorQueue = settings.ErrorQueueAddress();
            var account = CloudStorageAccount.Parse(connectionString);
            table = await TimeoutEntity.BuiltTimeoutTableWithExplicitName(connectionString, tableName, cancellationToken).ConfigureAwait(false);
            var container = account.CreateCloudBlobClient().GetContainerReference(table.Name.ToLower()); // TODO: can it be lowered?
            lockManager = new LockManager(container, LeaseLength);
            isAtMostOnce = settings.GetRequiredTransactionModeForReceives() == TransportTransactionMode.None;
        }
    }
}