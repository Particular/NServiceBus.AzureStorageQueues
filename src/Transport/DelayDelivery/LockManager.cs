namespace NServiceBus.Transport.AzureStorageQueues
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Azure;
    using global::Azure.Storage.Blobs;
    using global::Azure.Storage.Blobs.Models;
    using global::Azure.Storage.Blobs.Specialized;

    // Provides a container lease based lock manager.
    class LockManager
    {
        TimeSpan span;
        BlobContainerClient containerClient;
        BlobLeaseClient blobLeaseClient;
        bool created;
        BlobLease lease;

        public LockManager(BlobContainerClient containerClient, BlobLeaseClient blobLeaseClient, TimeSpan span)
        {
            this.containerClient = containerClient;
            this.blobLeaseClient = blobLeaseClient;
            this.span = span;
        }

        public async Task<bool> TryLockOrRenew(CancellationToken cancellationToken)
        {
            await EnsureContainerExists(cancellationToken).ConfigureAwait(false);

            if (lease == null)
            {
                try
                {
                    lease = (await blobLeaseClient.AcquireAsync(span, null, cancellationToken).ConfigureAwait(false)).Value;
                    return true;
                }
                catch (RequestFailedException exception) when (exception.Status == (int) HttpStatusCode.Conflict)
                {
                    // someone else raced for the lease and got it
                    return false;
                }
            }
            try
            {
                await blobLeaseClient.RenewAsync(null, cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch (RequestFailedException exception) when (exception.Status == (int) HttpStatusCode.Conflict)
            {
                // someone else raced for the lease and got it so we have to try to re-acquire it
                lease = null;
                return false;
            }
        }

        public async Task TryRelease(CancellationToken cancellationToken)
        {
            await EnsureContainerExists(cancellationToken).ConfigureAwait(false);

            if (lease != null)
            {
                try
                {
                    await blobLeaseClient.ReleaseAsync(null, cancellationToken).ConfigureAwait(false);
                }
                catch (RequestFailedException)
                {
                }
                finally
                {
                    lease = null;
                }
            }
        }

        async Task EnsureContainerExists(CancellationToken cancellationToken)
        {
            if (created == false)
            {
                await containerClient.CreateIfNotExistsAsync(PublicAccessType.BlobContainer, null, null, cancellationToken).ConfigureAwait(false);
                created = true;
            }
        }
    }
}