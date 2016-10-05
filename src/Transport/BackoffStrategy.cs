namespace NServiceBus.AzureStorageQueues
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    class BackoffStrategy
    {
        readonly TimeSpan peekInterval;
        readonly TimeSpan maximumWaitTimeWhenIdle;

        TimeSpan timeToDelayNextPeek;
        
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
            timeToDelayNextPeek = TimeSpan.Zero;
        }

        Task OnNothingProcessed(CancellationToken token)
        {
            if (timeToDelayNextPeek + peekInterval < maximumWaitTimeWhenIdle)
            {
                timeToDelayNextPeek += peekInterval;
            }
            else
            {
                timeToDelayNextPeek = maximumWaitTimeWhenIdle;
            }

            return Task.Delay(timeToDelayNextPeek, token);
        }

        public Task OnBatch(int requestedBatchSize, int receivedBatchSize, CancellationToken token)
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