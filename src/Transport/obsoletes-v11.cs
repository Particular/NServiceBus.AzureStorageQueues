namespace NServiceBus
{
    using System;
    using global::Azure.Storage.Queues;
    using Microsoft.Azure.Cosmos.Table;

    /// <summary>
    /// Provides methods to define routing between Azure Storage accounts and map them to a logical alias instead of using bare
    /// connection strings.
    /// </summary>
    partial class AccountRoutingSettings
    {
        /// <summary>
        /// Adds the mapping between the <paramref alias="alias" /> and its <paramref alias="connectionString" />.
        /// </summary>
        /// <remarks>Prefer to use the overload that accepts a <see cref="QueueServiceClient"/>.</remarks>
        [ObsoleteEx(
            Message = "Account aliases using connection strings have been deprecated. Use the AddAccount overload that accepts a QueueServiceClient instance.",
            TreatAsErrorFromVersion = "12.0",
            RemoveInVersion = "13.0")]
        public AccountInfo AddAccount(string alias, string connectionString) => AddAccount(alias, new QueueServiceClient(connectionString), CloudStorageAccount.Parse(connectionString).CreateCloudTableClient());
    }

    /// <summary>Configures native delayed delivery.</summary>
    public partial class DelayedDeliverySettings
    {
        /// <summary>
        /// Disables the Timeout Manager for the endpoint. Before disabling ensure there all timeouts in the timeout store
        /// have been processed or migrated.
        /// </summary>
        [ObsoleteEx(
            TreatAsErrorFromVersion = "11.0",
            RemoveInVersion = "12.0")]
        public void DisableTimeoutManager()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Disable delayed delivery.
        /// <remarks>
        /// Disabling delayed delivery reduces costs associated with polling Azure Storage service for delayed messages that need
        /// to be dispatched.
        /// Do not use this setting if your endpoint requires delayed messages, timeouts, or delayed retries.
        /// </remarks>
        /// </summary>
        [ObsoleteEx(
            Message = "Configure delayed delivery support via the AzureStorageQueueTransport constructor.",
            TreatAsErrorFromVersion = "11.0",
            RemoveInVersion = "12.0")]
        public void DisableDelayedDelivery()
        {
            throw new NotImplementedException();
        }
    }
}
