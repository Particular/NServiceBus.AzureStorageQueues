#pragma warning disable 1591

namespace NServiceBus
{
    using System;
    using global::Azure.Storage.Blobs;
    using global::Azure.Storage.Queues;


    public partial class AzureStorageQueueTransport
    {
        [ObsoleteEx(
            Message =
                "Use the constructor overload that accepts a TableServiceClient",
            TreatAsErrorFromVersion = "12.0",
            RemoveInVersion = "13.0")]
        public AzureStorageQueueTransport(QueueServiceClient queueServiceClient, BlobServiceClient blobServiceClient,
            object cloudTableClient) : base(TransportTransactionMode.None, false, false, false)
            => throw new NotImplementedException();
    }

    public partial class AccountRoutingSettings
    {
        [ObsoleteEx(
            Message =
                "Account aliases using connection strings have been deprecated. Use the AddAccount overload that accepts a QueueServiceClient instance.",
            TreatAsErrorFromVersion = "12.0",
            RemoveInVersion = "13.0")]
        public AccountInfo AddAccount(string alias, string connectionString)
            => throw new NotImplementedException();

        [ObsoleteEx(
            Message =
                "Use AddAccount that accepts a TableServiceClient",
            TreatAsErrorFromVersion = "12.0",
            RemoveInVersion = "13.0")]
        public AccountInfo AddAccount(string alias, QueueServiceClient queueServiceClient, object cloudTableClient)
            => throw new NotImplementedException();
    }

    public static partial class AzureStorageTransportExtensions
    {
        [ObsoleteEx(
            TreatAsErrorFromVersion = "12.0",
            RemoveInVersion = "13.0",
            ReplacementTypeOrMember = "Provide the TableServiceClient with the UseTransport<AzureStorageQueues> configuration as a parameter.")]
        public static TransportExtensions<AzureStorageQueueTransport> UseTransport<T>(this EndpointConfiguration config,
            QueueServiceClient queueServiceClient, BlobServiceClient blobServiceClient,
            object cloudTableClient)
            where T : AzureStorageQueueTransport
            => throw new NotImplementedException();

        [ObsoleteEx(
            Message =
                "Provide the TableServiceClient with the UseTransport<AzureStorageQueues> configuration as a parameter.",
            TreatAsErrorFromVersion = "12.0",
            RemoveInVersion = "13.0")]
        public static TransportExtensions<AzureStorageQueueTransport> UseCloudTableClient(
            this TransportExtensions<AzureStorageQueueTransport> config, object cloudTableClient)
            => throw new NotImplementedException();
    }

    public partial class AccountInfo
    {
        [ObsoleteEx(
            Message =
                "Use the constructor overload that accepts a TableServiceClient",
            TreatAsErrorFromVersion = "12.0",
            RemoveInVersion = "13.0")]
        public AccountInfo(string alias, QueueServiceClient queueServiceClient, object cloudTableClient)
            => throw new NotImplementedException();
    }
}

#pragma warning restore 1591