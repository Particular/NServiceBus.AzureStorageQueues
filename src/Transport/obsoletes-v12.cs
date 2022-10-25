#pragma warning disable 1591
#pragma warning disable IDE0060

namespace NServiceBus
{
    using System;
    using Azure.Transports.WindowsAzureStorageQueues;
    using Configuration.AdvancedExtensibility;
    using global::Azure.Data.Tables;
    using global::Azure.Storage.Blobs;
    using global::Azure.Storage.Queues;
    using global::Azure.Storage.Queues.Models;
    using Serialization;
    using Settings;

    partial class AccountInfo
    {
        [ObsoleteEx(
            Message = "The Microsoft.Azure.Cosmos.Table package has been deprecated. Use the constructor overload that accepts an Azure.Data.Tables.TableServiceClient instance.",
            TreatAsErrorFromVersion = "12.0",
            RemoveInVersion = "13.0")]
        public AccountInfo(string alias, QueueServiceClient queueServiceClient, object cloudTableClient)
            => throw new NotImplementedException();
    }

    partial class AccountRoutingSettings
    {
        [ObsoleteEx(
            Message = "Account aliases using connection strings have been deprecated. Use the AddAccount overload that accepts a QueueServiceClient instance.",
            TreatAsErrorFromVersion = "12.0",
            RemoveInVersion = "13.0")]
        public AccountInfo AddAccount(string alias, string connectionString)
            => throw new NotImplementedException();

        [ObsoleteEx(
            Message = "The Microsoft.Azure.Cosmos.Table package has been deprecated. Use the AddAccount overload that accepts an Azure.Data.Tables.TableServiceClient instance.",
            TreatAsErrorFromVersion = "12.0",
            RemoveInVersion = "13.0")]
        public AccountInfo AddAccount(string alias, QueueServiceClient queueServiceClient, object cloudTableClient)
            => throw new NotImplementedException();
    }

    partial class AzureStorageQueueTransport
    {
        [ObsoleteEx(
            Message = "The Microsoft.Azure.Cosmos.Table package has been deprecated. Use the constructor overload that accepts an Azure.Data.Tables.TableServiceClient instance.",
            TreatAsErrorFromVersion = "12.0",
            RemoveInVersion = "13.0")]
        public AzureStorageQueueTransport(QueueServiceClient queueServiceClient, BlobServiceClient blobServiceClient, object cloudTableClient)
            : base(TransportTransactionMode.ReceiveOnly, supportsDelayedDelivery: true, supportsPublishSubscribe: true, supportsTTBR: true)
            => throw new NotImplementedException();
    }

    public static class AzureStorageTransportExtensions
    {
        [ObsoleteEx(
            Message = "The Microsoft.Azure.Cosmos.Table package has been deprecated. Use the UseTransport<T> overload that accepts an Azure.Data.Tables.TableServiceClient instance.",
            TreatAsErrorFromVersion = "12.0",
            RemoveInVersion = "13.0")]
        public static TransportExtensions<AzureStorageQueueTransport> UseTransport<T>(this EndpointConfiguration config, QueueServiceClient queueServiceClient, BlobServiceClient blobServiceClient, object cloudTableClient)
            => throw new NotImplementedException();

        [ObsoleteEx(
            TreatAsErrorFromVersion = "12.0",
            RemoveInVersion = "13.0",
            ReplacementTypeOrMember = "EndpointConfiguration.UseTransport(TransportDefinition)")]
        public static TransportExtensions<AzureStorageQueueTransport> UseTransport<T>(this EndpointConfiguration config)
            where T : AzureStorageQueueTransport =>
            throw new NotImplementedException();

        [ObsoleteEx(
            TreatAsErrorFromVersion = "12.0",
            RemoveInVersion = "13.0",
            ReplacementTypeOrMember = "EndpointConfiguration.UseTransport(TransportDefinition)")]
        public static TransportExtensions<AzureStorageQueueTransport> UseTransport<T>(this EndpointConfiguration config,
            QueueServiceClient queueServiceClient)
            where T : AzureStorageQueueTransport =>
            throw new NotImplementedException();

        [ObsoleteEx(
            TreatAsErrorFromVersion = "12.0",
            RemoveInVersion = "13.0",
            ReplacementTypeOrMember = "EndpointConfiguration.UseTransport(TransportDefinition)")]
        public static TransportExtensions<AzureStorageQueueTransport> UseTransport<T>(this EndpointConfiguration config,
            QueueServiceClient queueServiceClient, BlobServiceClient blobServiceClient,
            TableServiceClient tableServiceClient)
            where T : AzureStorageQueueTransport =>
            throw new NotImplementedException();

        [ObsoleteEx(
            Message = "Configure the transport via the AzureStorageQueueTransport MessageInvisibleTime property",
            TreatAsErrorFromVersion = "12.0",
            RemoveInVersion = "13.0")]
        public static TransportExtensions<AzureStorageQueueTransport> MessageInvisibleTime(
            this TransportExtensions<AzureStorageQueueTransport> config, TimeSpan value) =>
            throw new NotImplementedException();

        [ObsoleteEx(
            Message = "Configure the transport via the AzureStorageQueueTransport PeekInterval property",
            TreatAsErrorFromVersion = "12.0",
            RemoveInVersion = "13.0")]
        public static TransportExtensions<AzureStorageQueueTransport> PeekInterval(
            this TransportExtensions<AzureStorageQueueTransport> config, TimeSpan value) =>
            throw new NotImplementedException();

        [ObsoleteEx(
            Message = "Configure the transport via the AzureStorageQueueTransport MaximumWaitTimeWhenIdle property",
            TreatAsErrorFromVersion = "12.0",
            RemoveInVersion = "13.0")]
        public static TransportExtensions<AzureStorageQueueTransport> MaximumWaitTimeWhenIdle(
            this TransportExtensions<AzureStorageQueueTransport> config, TimeSpan value) =>
            throw new NotImplementedException();

        [ObsoleteEx(
            Message = "Configure the transport via the AzureStorageQueueTransport QueueNameSanitizer property",
            TreatAsErrorFromVersion = "12.0",
            RemoveInVersion = "13.0")]
        public static TransportExtensions<AzureStorageQueueTransport> SanitizeQueueNamesWith(
            this TransportExtensions<AzureStorageQueueTransport> config,
            Func<string, string> queueNameSanitizer) =>
            throw new NotImplementedException();

        [ObsoleteEx(
            Message = "Configure the transport via the AzureStorageQueueTransport ReceiverBatchSize property",
            TreatAsErrorFromVersion = "12.0",
            RemoveInVersion = "13.0")]
        public static TransportExtensions<AzureStorageQueueTransport> BatchSize(
            this TransportExtensions<AzureStorageQueueTransport> config, int value) =>
            throw new NotImplementedException();

        [ObsoleteEx(
            Message = "Configure the transport via the AzureStorageQueueTransport DegreeOfReceiveParallelism property",
            TreatAsErrorFromVersion = "12.0",
            RemoveInVersion = "13.0")]
        public static TransportExtensions<AzureStorageQueueTransport> DegreeOfReceiveParallelism(
            this TransportExtensions<AzureStorageQueueTransport> config, int degreeOfReceiveParallelism) =>
            throw new NotImplementedException();

        [ObsoleteEx(
            Message =
                "Configure the transport via the AzureStorageQueueTransport MessageWrapperSerializationDefinition property",
            TreatAsErrorFromVersion = "12.0",
            RemoveInVersion = "13.0")]
        public static TransportExtensions<AzureStorageQueueTransport>
            SerializeMessageWrapperWith<TSerializationDefinition>(
                this TransportExtensions<AzureStorageQueueTransport> config)
            where TSerializationDefinition : SerializationDefinition, new() =>
            throw new NotImplementedException();

        [ObsoleteEx(
            Message = "Configure the transport via the AzureStorageQueueTransport MessageUnwrapper property",
            TreatAsErrorFromVersion = "12.0",
            RemoveInVersion = "13.0")]
        public static TransportExtensions<AzureStorageQueueTransport> UnwrapMessagesWith(
            this TransportExtensions<AzureStorageQueueTransport> config,
            Func<QueueMessage, MessageWrapper> unwrapper) =>
            throw new NotImplementedException();

        [ObsoleteEx(
            Message = "Configure the transport via the AzureStorageQueueTransport Subscription property",
            TreatAsErrorFromVersion = "12.0",
            RemoveInVersion = "13.0")]
        public static TransportExtensions<AzureStorageQueueTransport> DisableCaching(
            this TransportExtensions<AzureStorageQueueTransport> config) =>
            throw new NotImplementedException();

        [ObsoleteEx(
            Message = "Configure the transport via the AzureStorageQueueTransport Subscription property",
            TreatAsErrorFromVersion = "12.0",
            RemoveInVersion = "13.0")]
        public static TransportExtensions<AzureStorageQueueTransport> CacheInvalidationPeriod(
            this TransportExtensions<AzureStorageQueueTransport> config,
            TimeSpan cacheInvalidationPeriod) =>
            throw new NotImplementedException();

        [ObsoleteEx(
            Message =
                "Configure the transport connection string via the AzureStorageQueueTransport instance constructor",
            TreatAsErrorFromVersion = "12.0",
            RemoveInVersion = "13.0")]
        public static TransportExtensions<AzureStorageQueueTransport> ConnectionString(
            this TransportExtensions<AzureStorageQueueTransport> config, string connectionString) =>
            throw new NotImplementedException();

        [ObsoleteEx(
            Message =
                "Configure the transport connection string via the AzureStorageQueueTransport instance constructor",
            TreatAsErrorFromVersion = "12.0",
            RemoveInVersion = "13.0")]
        public static TransportExtensions<AzureStorageQueueTransport> ConnectionString(
            this TransportExtensions<AzureStorageQueueTransport> config, Func<string> connectionString) =>
            throw new NotImplementedException();

        [ObsoleteEx(
            Message = "Configure the transport via the AzureStorageQueueTransport DelayedDelivery property",
            TreatAsErrorFromVersion = "12.0",
            RemoveInVersion = "13.0")]
        public static DelayedDeliverySettings DelayedDelivery(
            this TransportExtensions<AzureStorageQueueTransport> config) =>
        throw new NotImplementedException();

        [ObsoleteEx(
            Message = "Configure the transport via the AzureStorageQueueTransport AccountRouting property",
            TreatAsErrorFromVersion = "12.0",
            RemoveInVersion = "13.0")]
        public static AccountRoutingSettings AccountRouting(
            this TransportExtensions<AzureStorageQueueTransport> config) =>
            throw new NotImplementedException();

        [ObsoleteEx(
            Message =
                "Configure the transport via the AzureStorageQueueTransport AccountRouting.DefaultAccountAlias property",
            TreatAsErrorFromVersion = "12.0",
            RemoveInVersion = "13.0")]
        public static TransportExtensions<AzureStorageQueueTransport> DefaultAccountAlias(
            this TransportExtensions<AzureStorageQueueTransport> config, string alias) =>
            throw new NotImplementedException();

        [ObsoleteEx(
            Message = "Configure the transport via the AzureStorageQueueTransport Subscription property",
            TreatAsErrorFromVersion = "12.0",
            RemoveInVersion = "13.0")]
        public static TransportExtensions<AzureStorageQueueTransport> SubscriptionTableName(

            this TransportExtensions<AzureStorageQueueTransport> config, string subscriptionTableName) =>
            throw new NotImplementedException();
    }

    public class DelayedDeliverySettings : ExposeSettings
    {
        internal DelayedDeliverySettings(NativeDelayedDeliverySettings transportDelayedDelivery) : base(new SettingsHolder())
            => throw new NotImplementedException();

        [ObsoleteEx(
            Message =
                "Configure the transport via the AzureStorageQueueTransport DelayedDelivery.DelayedDeliveryTableName property",
            TreatAsErrorFromVersion = "12.0",
            RemoveInVersion = "13.0")]
        public void UseTableName(string delayedMessagesTableName)
            => throw new NotImplementedException();
    }
}

#pragma warning restore 1591
#pragma warning restore IDE0060