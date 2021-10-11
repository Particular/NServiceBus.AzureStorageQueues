namespace NServiceBus
{
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
}
