#pragma warning disable 1591

namespace NServiceBus
{
    using System;
    using global::Azure.Data.Tables;
    using global::Azure.Storage.Queues;

    partial class AccountRoutingSettings
    {
        [ObsoleteEx(
            Message =
                "Account aliases using connection strings have been deprecated. Use the AddAccount overload that accepts a QueueServiceClient instance.",
            TreatAsErrorFromVersion = "12.0",
            RemoveInVersion = "13.0")]
        public AccountInfo AddAccount(string alias, string connectionString) => AddAccount(alias,
            new QueueServiceClient(connectionString),
            new TableServiceClient(connectionString));
    }

    public partial class DelayedDeliverySettings
    {
        [ObsoleteEx(
            Message = "The TimeoutManager has been deprecated.",
            TreatAsErrorFromVersion = "11.0",
            RemoveInVersion = "12.0")]
        public void DisableTimeoutManager()
            => throw new NotImplementedException();

        [ObsoleteEx(
            Message = "Configure delayed delivery support via the AzureStorageQueueTransport constructor.",
            TreatAsErrorFromVersion = "11.0",
            RemoveInVersion = "12.0")]
        public void DisableDelayedDelivery()
            => throw new NotImplementedException();
    }
}

#pragma warning restore 1591