﻿#pragma warning disable CS0618 // Type or member is obsolete

namespace NServiceBus
{
    using System;
    using System.Collections.Generic;
    using global::Azure.Storage.Blobs;
    using global::Azure.Storage.Queues;
    using global::Azure.Storage.Queues.Models;
    using Microsoft.Azure.Cosmos.Table;
    using NServiceBus.Azure.Transports.WindowsAzureStorageQueues;
    using NServiceBus.Configuration.AdvancedExtensibility;
    using NServiceBus.Serialization;
    using NServiceBus.Settings;

    static partial class AzureStorageTransportExtensions
    {
        /// <summary>
        /// Controls how long messages should be invisible to other callers when receiving messages from the queue
        /// </summary>
        [ObsoleteEx(
            Message = "Configure the transport via the TransportDefinition instance's properties",
            TreatAsErrorFromVersion = "10.0",
            RemoveInVersion = "11.0")]
        public static TransportExtensions<AzureStorageQueueTransport> MessageInvisibleTime(this TransportExtensions<AzureStorageQueueTransport> config, TimeSpan value)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sets the amount of time to add to the time to wait before checking for a new message
        /// </summary>
        [ObsoleteEx(
            Message = "Configure the transport via the TransportDefinition instance's properties",
            TreatAsErrorFromVersion = "10.0",
            RemoveInVersion = "11.0")]
        public static TransportExtensions<AzureStorageQueueTransport> PeekInterval(this TransportExtensions<AzureStorageQueueTransport> config, TimeSpan value)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sets the maximum amount of time, in milliseconds, that the queue will wait before checking for a new message
        /// </summary>
        [ObsoleteEx(
            Message = "Configure the transport via the TransportDefinition instance's properties",
            TreatAsErrorFromVersion = "10.0",
            RemoveInVersion = "11.0")]
        public static TransportExtensions<AzureStorageQueueTransport> MaximumWaitTimeWhenIdle(this TransportExtensions<AzureStorageQueueTransport> config, TimeSpan value)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Registers a queue name sanitizer to apply to queue names not compliant wth Azure Storage Queue naming rules.
        /// <remarks>By default no sanitization is performed.</remarks>
        /// </summary>
        [ObsoleteEx(
            Message = "Configure the transport via the TransportDefinition instance's properties",
            TreatAsErrorFromVersion = "10.0",
            RemoveInVersion = "11.0")]
        public static TransportExtensions<AzureStorageQueueTransport> SanitizeQueueNamesWith(this TransportExtensions<AzureStorageQueueTransport> config, Func<string, string> queueNameSanitizer)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Controls how many messages should be read from the queue at once
        /// </summary>
        [ObsoleteEx(
            Message = "Configure the transport via the TransportDefinition instance's properties",
            TreatAsErrorFromVersion = "10.0",
            RemoveInVersion = "11.0")]
        public static TransportExtensions<AzureStorageQueueTransport> BatchSize(this TransportExtensions<AzureStorageQueueTransport> config, int value)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sets the degree of parallelism that should be used to receive messages.
        /// </summary>
        [ObsoleteEx(
            Message = "Configure the transport via the TransportDefinition instance's properties",
            TreatAsErrorFromVersion = "10.0",
            RemoveInVersion = "11.0")]
        public static TransportExtensions<AzureStorageQueueTransport> DegreeOfReceiveParallelism(this TransportExtensions<AzureStorageQueueTransport> config, int degreeOfReceiveParallelism)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sets a custom serialization for <see cref="MessageWrapper" />.
        /// </summary>
        [ObsoleteEx(
            Message = "Configure the transport via the TransportDefinition instance's properties",
            TreatAsErrorFromVersion = "10.0",
            RemoveInVersion = "11.0")]
        public static TransportExtensions<AzureStorageQueueTransport> SerializeMessageWrapperWith<TSerializationDefinition>(this TransportExtensions<AzureStorageQueueTransport> config)
            where TSerializationDefinition : SerializationDefinition, new()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Registers a custom unwrapper to convert native messages to <see cref="MessageWrapper" />. This is needed when receiving raw json/xml/etc messages from non NServiceBus endpoints.
        /// </summary>
        [ObsoleteEx(
            Message = "Configure the transport via the TransportDefinition instance's properties",
            TreatAsErrorFromVersion = "10.0",
            RemoveInVersion = "11.0")]
        public static TransportExtensions<AzureStorageQueueTransport> UnwrapMessagesWith(this TransportExtensions<AzureStorageQueueTransport> config, Func<QueueMessage, MessageWrapper> unwrapper)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sets <see cref="QueueServiceClient"/> to be used for messaging operations.
        /// </summary>
        [ObsoleteEx(
            Message = "Configure the transport via the TransportDefinition instance constructor",
            TreatAsErrorFromVersion = "10.0",
            RemoveInVersion = "11.0")]
        public static TransportExtensions<AzureStorageQueueTransport> UseQueueServiceClient(this TransportExtensions<AzureStorageQueueTransport> config, QueueServiceClient queueServiceClient)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sets <see cref="QueueServiceClient"/> to be used for delayed delivery feature.
        /// </summary>
        [ObsoleteEx(
            Message = "Configure the transport via the TransportDefinition instance constructor",
            TreatAsErrorFromVersion = "10.0",
            RemoveInVersion = "11.0")]
        public static TransportExtensions<AzureStorageQueueTransport> UseBlobServiceClient(this TransportExtensions<AzureStorageQueueTransport> config, BlobServiceClient blobServiceClient)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sets <see cref="CloudTableClient"/> to be used for delayed delivery feature.
        /// </summary>
        [ObsoleteEx(
            Message = "Configure the transport via the TransportDefinition instance constructor",
            TreatAsErrorFromVersion = "10.0",
            RemoveInVersion = "11.0")]
        public static TransportExtensions<AzureStorageQueueTransport> UseCloudTableClient(this TransportExtensions<AzureStorageQueueTransport> config, CloudTableClient cloudTableClient)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Configures delayed delivery features of this transport.
        /// </summary>
        [ObsoleteEx(
            Message = "Configure the transport via the TransportDefinition instance constructor",
            TreatAsErrorFromVersion = "10.0",
            RemoveInVersion = "11.0")]
        public static DelayedDeliverySettings DelayedDelivery(this TransportExtensions<AzureStorageQueueTransport> config)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Provides access to configure cross account routing.
        /// </summary>
        [ObsoleteEx(
            Message = "Configure the transport via the TransportDefinition instance's properties",
            TreatAsErrorFromVersion = "10.0",
            RemoveInVersion = "11.0")]
        public static AccountRoutingSettings AccountRouting(this TransportExtensions<AzureStorageQueueTransport> transportExtensions)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Set default account alias.
        /// </summary>
        [ObsoleteEx(
            Message = "Configure the transport via the TransportDefinition instance's properties",
            TreatAsErrorFromVersion = "10.0",
            RemoveInVersion = "11.0")]
        public static TransportExtensions<AzureStorageQueueTransport> DefaultAccountAlias(this TransportExtensions<AzureStorageQueueTransport> transportExtensions, string alias)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>Configures native delayed delivery.</summary>
    public partial class DelayedDeliverySettings : ExposeSettings
    {
        internal DelayedDeliverySettings(SettingsHolder settings) : base(settings) { }

        /// <summary>Override the default table name used for storing delayed messages.</summary>
        /// <param name="delayedMessagesTableName">New table name.</param>
        [ObsoleteEx(
            Message = "Configure the transport via the TransportDefinition instance's properties",
            TreatAsErrorFromVersion = "10.0",
            RemoveInVersion = "11.0")]
        public void UseTableName(string delayedMessagesTableName)
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
            Message = "Configure the transport via the TransportDefinition constructor.",
            TreatAsErrorFromVersion = "10.0",
            RemoveInVersion = "11.0")]
        public void DisableDelayedDelivery()
        {
            throw new NotImplementedException();
        }
    }

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

#pragma warning restore CS0618 // Type or member is obsolete
