namespace NServiceBus.Azure.Transports.WindowsAzureStorageQueues.AcceptanceTests.DelayDelivery
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Azure.Storage.Blobs;
    using global::Azure.Storage.Blobs.Specialized;
    using NUnit.Framework;
    using Transport.AzureStorageQueues;

    public class LockManagerTests
    {
        BlobServiceClient blobService;

        [OneTimeSetUp]
        public void SetUp()
        {
            blobService = new BlobServiceClient(Testing.Utilities.GetEnvConfiguredConnectionString());
        }

        [Test]
        public async Task WhenLeaseTaken_ThenItCanBeRenewedManyTimes()
        {
            var manager = GetLockManager("a156ef954f9594f51b24392d0df5e7771");

            const int manyTimes = 10;
            for (var i = 0; i < manyTimes; i++)
            {
                Assert.IsTrue(await manager.TryLockOrRenew(CancellationToken.None).ConfigureAwait(false));
            }
        }

        [Test]
        public async Task WhenLeaseTaken_ThenNoOtherLeaseCanBeTaken()
        {
            const string id = "a99173943b8c74a00bff8fd1d850665fb";
            var manager1 = GetLockManager(id);
            var manager2 = GetLockManager(id);

            await manager1.TryLockOrRenew(CancellationToken.None).ConfigureAwait(false);
            Assert.IsFalse(await manager2.TryLockOrRenew(CancellationToken.None).ConfigureAwait(false));
        }

        [Test]
        public async Task WhenLeaseReleased_ThenAnotherCanBeTaken()
        {
            const string id = "a9a8ca20acb1f43b19415eba8997be991";
            var manager1 = GetLockManager(id);
            var manager2 = GetLockManager(id);

            await manager1.TryLockOrRenew(CancellationToken.None).ConfigureAwait(false);
            await manager1.TryRelease(CancellationToken.None).ConfigureAwait(false);
            Assert.IsTrue(await manager2.TryLockOrRenew(CancellationToken.None).ConfigureAwait(false));
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