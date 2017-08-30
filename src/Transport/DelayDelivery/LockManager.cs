namespace NServiceBus.Azure.Transports.WindowsAzureStorageQueues.DelayDelivery
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Blob;
    using Microsoft.WindowsAzure.Storage.Blob.Protocol;

    // Provides a container lease based lock manager.
    class LockManager
    {
        TimeSpan span;
        CloudBlobContainer container;
        bool created;
        string lease;

        public LockManager(CloudBlobContainer container, TimeSpan span)
        {
            this.container = container;
            this.span = span;
        }

        public async Task<bool> TryLockOrRenew(CancellationToken cancellationToken)
        {
            await EnsureContainerExists(cancellationToken).ConfigureAwait(false);

            if (lease == null)
            {
                try
                {
                    lease = await container.AcquireLeaseAsync(span, null, null, null, null, cancellationToken).ConfigureAwait(false);
                    return true;
                }
                catch (StorageException exception)
                    when (exception.RequestInformation.HttpStatusCode == (int) HttpStatusCode.Conflict)
                {
                    return false;
                }
            }
            try
            {
                await container.RenewLeaseAsync(AccessCondition.GenerateLeaseCondition(lease), null, null, cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch (StorageException exception)
                when (exception.RequestInformation.ExtendedErrorInformation.ErrorCode == BlobErrorCodeStrings.LeaseIdMismatchWithLeaseOperation)
            {
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
                    await container.ReleaseLeaseAsync(AccessCondition.GenerateLeaseCondition(lease), null, null, cancellationToken).ConfigureAwait(false);
                }
                catch (StorageException)
                {
                }
            }
        }

        async Task EnsureContainerExists(CancellationToken cancellationToken)
        {
            if (created == false)
            {
                await container.CreateIfNotExistsAsync(BlobContainerPublicAccessType.Container, null,null, cancellationToken).ConfigureAwait(false);
                created = true;
            }
        }
    }
}