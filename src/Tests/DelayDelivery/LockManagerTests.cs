namespace NServiceBus.Transport.AzureStorageQueues.Tests
{
    using System;
    using System.Threading.Tasks;
    using AzureStorageQueues;
    using global::Azure.Storage.Blobs;
    using global::Azure.Storage.Blobs.Specialized;
    using NUnit.Framework;

    public class LockManagerTests
    {
        BlobServiceClient blobService;

        [OneTimeSetUp]
        public void SetUp() => blobService = new BlobServiceClient(Testing.Utilities.GetEnvConfiguredConnectionString());

        [Test]
        public async Task WhenLeaseTaken_ThenItCanBeRenewedManyTimes()
        {
            string id = Guid.NewGuid().ToString("n");
            var manager = GetLockManager(id);

            const int manyTimes = 10;
            for (var i = 0; i < manyTimes; i++)
            {
                Assert.That(await manager.TryLockOrRenew().ConfigureAwait(false), Is.True);
            }
        }

        [Test]
        public async Task WhenLeaseTaken_ThenNoOtherLeaseCanBeTaken()
        {
            string id = Guid.NewGuid().ToString("n");
            var manager1 = GetLockManager(id);
            var manager2 = GetLockManager(id);

            await manager1.TryLockOrRenew().ConfigureAwait(false);
            Assert.That(await manager2.TryLockOrRenew().ConfigureAwait(false), Is.False);
        }

        [Test]
        public async Task WhenLeaseReleased_ThenAnotherCanBeTaken()
        {
            string id = Guid.NewGuid().ToString("n");
            var manager1 = GetLockManager(id);
            var manager2 = GetLockManager(id);

            await manager1.TryLockOrRenew().ConfigureAwait(false);
            await manager1.TryRelease().ConfigureAwait(false);
            Assert.That(await manager2.TryLockOrRenew().ConfigureAwait(false), Is.True);
        }

        LockManager GetLockManager(string containerName)
        {
            var container = blobService.GetBlobContainerClient(containerName);
            var blobLease = new BlobLeaseClient(container);
            var manager = new LockManager(container, blobLease, TimeSpan.FromSeconds(20));
            return manager;
        }
    }
}