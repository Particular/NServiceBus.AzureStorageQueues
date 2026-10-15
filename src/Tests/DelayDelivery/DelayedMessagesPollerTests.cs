namespace NServiceBus.Transport.AzureStorageQueues.Tests
{
    using System;
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;
    using AzureStorageQueues;
    using global::Azure.Core;
    using global::Azure.Core.Pipeline;
    using global::Azure.Data.Tables;
    using global::Azure.Storage.Blobs;
    using global::Azure.Storage.Sas;
    using NUnit.Framework;

    public class DelayedMessagesPollerTests
    {
        [Test]
        public async Task WhenLockCannotBeAcquired_ShouldBackOff()
        {
            var connectionString = Testing.Utilities.GetEnvConfiguredConnectionString();

            // without the create permission, creating the lock container fails with 403 on every attempt
            var sasUri = new BlobServiceClient(connectionString).GenerateAccountSasUri(
                AccountSasPermissions.Read | AccountSasPermissions.List,
                DateTimeOffset.UtcNow.AddHours(1),
                AccountSasResourceTypes.Service | AccountSasResourceTypes.Container | AccountSasResourceTypes.Object);

            var attempts = new AttemptRecorder(attemptsToWaitFor: 3);
            var options = new BlobClientOptions();
            options.AddPolicy(attempts, HttpPipelinePosition.PerRetry);

            var poller = new DelayedMessagesPoller(
                new TableClient(connectionString, "delays" + Guid.NewGuid().ToString("n")),
                new BlobServiceClient(sasUri, options),
                errorQueueAddress: "error",
                isAtMostOnce: false,
                dispatcher: null,
                new BackoffStrategy(TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(1)));

            poller.Start();
            try
            {
                await attempts.Completed.WaitAsync(TimeSpan.FromSeconds(30));
            }
            finally
            {
                await poller.Stop();
            }

            var sentAt = attempts.SentAt.Take(3).ToArray();
            var gaps = sentAt.Zip(sentAt.Skip(1), (previous, next) => next - previous);

            Assert.That(attempts.StatusCodes.Take(3), Is.All.EqualTo(403));
            // the backoff waits 500 ms after the first failure and 1 s after the second,
            // without it the next attempt follows after one round trip
            Assert.That(gaps, Is.All.GreaterThanOrEqualTo(TimeSpan.FromMilliseconds(400)));
        }

        class AttemptRecorder(int attemptsToWaitFor) : HttpPipelineSynchronousPolicy
        {
            public ConcurrentQueue<TimeSpan> SentAt { get; } = new();
            public ConcurrentQueue<int> StatusCodes { get; } = new();
            public Task Completed => completed.Task;

            public override void OnSendingRequest(HttpMessage message) => SentAt.Enqueue(stopwatch.Elapsed);

            public override void OnReceivedResponse(HttpMessage message)
            {
                StatusCodes.Enqueue(message.Response.Status);

                if (StatusCodes.Count >= attemptsToWaitFor)
                {
                    completed.TrySetResult();
                }
            }

            readonly Stopwatch stopwatch = Stopwatch.StartNew();
            readonly TaskCompletionSource completed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }
}