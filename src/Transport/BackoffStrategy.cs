namespace NServiceBus.Transport.AzureStorageQueues
{
    using NServiceBus.Logging;
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    class BackoffStrategy
    {
        readonly TimeSpan peekInterval;
        readonly TimeSpan maximumWaitTimeWhenIdle;

        TimeSpan timeToDelayUntilNextPeek;

        static readonly ILog Logger = LogManager.GetLogger<BackoffStrategy>();

        /// <summary>
        /// </summary>
        /// <param name="peekInterval">The amount of time, in milliseconds, to add to the time to wait before checking for a new message</param>
        /// <param name="maximumWaitTimeWhenIdle">The maximum amount of time that the queue will wait before checking for a new message</param>
        public BackoffStrategy(TimeSpan peekInterval, TimeSpan maximumWaitTimeWhenIdle)
        {
            this.peekInterval = peekInterval;
            this.maximumWaitTimeWhenIdle = maximumWaitTimeWhenIdle;
        }

        void OnSomethingProcessed()
        {
            Logger.Debug("Processed message, setting delay until next peek to 0");
            timeToDelayUntilNextPeek = TimeSpan.Zero;
        }

        Task OnNothingProcessed(CancellationToken token)
        {
            Logger.Debug("Nothing processed, increasing delay until next peek");

            if (timeToDelayUntilNextPeek + peekInterval < maximumWaitTimeWhenIdle)
            {
                timeToDelayUntilNextPeek += peekInterval;
            }
            else
            {
                timeToDelayUntilNextPeek = maximumWaitTimeWhenIdle;
            }

            return Task.Delay(timeToDelayUntilNextPeek, token);
        }

        public Task OnBatch(int receivedBatchSize, CancellationToken token)
        {
            if (receivedBatchSize > 0)
            {
                OnSomethingProcessed();
                return TaskEx.CompletedTask;
            }

            return OnNothingProcessed(token);
        }
    }
}