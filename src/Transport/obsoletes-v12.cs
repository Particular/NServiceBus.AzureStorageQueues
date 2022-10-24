#pragma warning disable 1591

namespace NServiceBus
{
    using System;
    using global::Azure.Data.Tables;
    using global::Azure.Storage.Blobs;
    using global::Azure.Storage.Queues;

    partial class AccountInfo
    {
        [ObsoleteEx(
            Message = "The Microsoft.Azure.Cosmos.Table package has been deprecated. Use the constructor overload that accepts an Azure.Data.Tables.TableServiceClient instance.",
            TreatAsErrorFromVersion = "12.0",
            RemoveInVersion = "13.0")]
        public AccountInfo(string alias, QueueServiceClient queueServiceClient, object cloudTableClient) => throw new NotImplementedException();
    }

    partial class AccountRoutingSettings
    {
        [ObsoleteEx(
            Message = "Account aliases using connection strings have been deprecated. Use the AddAccount overload that accepts a QueueServiceClient instance.",
            TreatAsErrorFromVersion = "12.0",
            RemoveInVersion = "13.0")]
        public AccountInfo AddAccount(string alias, string connectionString) => AddAccount(alias,
            new QueueServiceClient(connectionString),
            new TableServiceClient(connectionString));

        [ObsoleteEx(
            Message = "The Microsoft.Azure.Cosmos.Table package has been deprecated. Use the AddAccount overload that accepts an Azure.Data.Tables.TableServiceClient instance.",
            TreatAsErrorFromVersion = "12.0",
            RemoveInVersion = "13.0")]
        public AccountInfo AddAccount(string alias, QueueServiceClient queueServiceClient, object cloudTableClient) => throw new NotImplementedException();
    }

    partial class AzureStorageQueueTransport
    {
        [ObsoleteEx(
            Message = "The Microsoft.Azure.Cosmos.Table package has been deprecated. Use the constructor overload that accepts an Azure.Data.Tables.TableServiceClient instance.",
            TreatAsErrorFromVersion = "12.0",
            RemoveInVersion = "13.0")]
        public AzureStorageQueueTransport(QueueServiceClient queueServiceClient, BlobServiceClient blobServiceClient, object cloudTableClient)
            : base(TransportTransactionMode.ReceiveOnly, supportsDelayedDelivery: true, supportsPublishSubscribe: true, supportsTTBR: true) => throw new NotImplementedException();
    }

    static partial class AzureStorageTransportExtensions
    {
        [ObsoleteEx(
            Message = "The Microsoft.Azure.Cosmos.Table package has been deprecated. Use the UseTransport<T> overload that accepts an Azure.Data.Tables.TableServiceClient instance.",
            TreatAsErrorFromVersion = "12.0",
            RemoveInVersion = "13.0")]
        public static TransportExtensions<AzureStorageQueueTransport> UseTransport<T>(this EndpointConfiguration config, QueueServiceClient queueServiceClient, BlobServiceClient blobServiceClient, object cloudTableClient)
            => throw new NotImplementedException();
    }
}

#pragma warning restore 1591