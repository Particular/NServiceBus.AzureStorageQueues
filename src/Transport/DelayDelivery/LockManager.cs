namespace NServiceBus.Azure.Transports.WindowsAzureStorageQueues.DelayDelivery
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Blob;
    using Microsoft.WindowsAzure.Storage.Blob.Protocol;

    sealed class LockManager
    {
        readonly TimeSpan span;
        readonly CloudBlobContainer container;
        bool _created;
        string _lease;

        public LockManager(CloudBlobContainer container, TimeSpan span)
        {
            this.container = container;
            this.span = span;
        }

        public async Task<bool> TryLockOrRenew()
        {
            await EnsureContainerExists().ConfigureAwait(false);

            if (_lease == null)
            {
                try
                {
                    _lease = await container.AcquireLeaseAsync(span).ConfigureAwait(false);
                    return true;
                }
                catch (StorageException ex)
                {
                    if (ex.RequestInformation.ExtendedErrorInformation.ErrorCode == BlobErrorCodeStrings.LeaseAlreadyPresent)
                    {
                        return false;
                    }
                    throw;
                }
            }
            else
            {
                try
                {
                    await container.RenewLeaseAsync(AccessCondition.GenerateLeaseCondition(_lease)).ConfigureAwait(false);
                    return true;
                }
                catch (StorageException ex)
                {
                    if (ex.RequestInformation.ExtendedErrorInformation.ErrorCode == BlobErrorCodeStrings.LeaseIdMismatchWithLeaseOperation)
                    {
                        _lease = null;
                        return false;
                    }
                    throw;
                }
            }
        }

        public async Task TryRelease()
        {
            await EnsureContainerExists().ConfigureAwait(false);

            if (_lease != null)
            {
                try
                {
                    await container.ReleaseLeaseAsync(AccessCondition.GenerateLeaseCondition(_lease)).ConfigureAwait(false);
                }
                catch (StorageException)
                {
                }
            }
        }

        async Task EnsureContainerExists()
        {
            if (_created == false)
            {
                await container.CreateIfNotExistsAsync().ConfigureAwait(false);
                _created = true;
            }
        }
    }
}