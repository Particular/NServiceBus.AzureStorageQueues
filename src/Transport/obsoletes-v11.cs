namespace NServiceBus
{
    using System;
    using System.Collections.Generic;
    using global::Azure.Storage.Queues;

    /// <summary>
    /// Provides methods to define routing between Azure Storage accounts and map them to a logical alias instead of using bare
    /// connection strings.
    /// </summary>
    [ObsoleteEx(
        Message = "Account aliases have been deprecated. Use the TransportDefinition constructor that accepts fully customized Azure service clients.",
        TreatAsErrorFromVersion = "11.0",
        RemoveInVersion = "12.0")]
    public class AccountRoutingSettings
    {
        internal AccountRoutingSettings()
        {

        }

        /// <summary>
        /// Get or set default account alias.
        /// </summary>
        [ObsoleteEx(
            Message = "Account aliases have been deprecated. Use the TransportDefinition constructor that accepts fully customized Azure service clients.",
            TreatAsErrorFromVersion = "11.0",
            RemoveInVersion = "12.0")]
        public string DefaultAccountAlias
        {
            get => _defaultAccountAlias;
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new ArgumentException("Should not be null or white space", nameof(DefaultAccountAlias));
                }
                _defaultAccountAlias = value;
            }
        }

        /// <summary>
        /// Adds the mapping between the <paramref alias="alias" /> and its <paramref alias="connectionString" />.
        /// </summary>
        /// <remarks>Prefer to use the overload that accepts a <see cref="QueueServiceClient"/>.</remarks>
        [ObsoleteEx(
            Message = "Account aliases have been deprecated. Use the TransportDefinition constructor that accepts fully customized Azure service clients.",
            TreatAsErrorFromVersion = "11.0",
            RemoveInVersion = "12.0")]
        public AccountInfo AddAccount(string alias, string connectionString) => AddAccount(alias, new QueueServiceClient(connectionString));

        /// <summary>
        /// Adds the mapping between the <paramref alias="alias" /> and its <paramref alias="QueueServiceClient" />.
        /// </summary>
        [ObsoleteEx(
            Message = "Account aliases have been deprecated. Use the TransportDefinition constructor that accepts fully customized Azure service clients.",
            TreatAsErrorFromVersion = "11.0",
            RemoveInVersion = "12.0")]
        public AccountInfo AddAccount(string alias, QueueServiceClient connectionClient)
        {
            if (mappings.TryGetValue(alias, out var accountInfo))
            {
                return accountInfo;
            }

            accountInfo = new AccountInfo(alias, connectionClient);
            mappings.Add(alias, accountInfo);

            return accountInfo;
        }

        internal Dictionary<string, AccountInfo> mappings = new Dictionary<string, AccountInfo>();
        string _defaultAccountAlias;
    }
}
