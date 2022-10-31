#pragma warning disable 1591

namespace NServiceBus
{
    using System;

    partial class AccountRoutingSettings
    {
        [ObsoleteEx(
            Message =
                "Account aliases using connection strings have been deprecated. Use the AddAccount overload that accepts a QueueServiceClient instance.",
            TreatAsErrorFromVersion = "12.0",
            RemoveInVersion = "13.0")]
        public AccountInfo AddAccount(string alias, string connectionString)
            => throw new NotImplementedException();
    }
}

#pragma warning restore 1591