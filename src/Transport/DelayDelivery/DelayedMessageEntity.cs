﻿namespace NServiceBus.Transport.AzureStorageQueues
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using global::Azure;
    using global::Azure.Data.Tables;
    using Transport;

    /// <summary>
    /// Represents a record in the native delays storage table which can be deferred message, saga timeouts, and delayed retries.
    /// </summary>
    class DelayedMessageEntity : ITableEntity
    {
        public string Destination { get; set; }
        public byte[] Body { get; set; }
        public string MessageId { get; set; }
        public string Headers { get; set; }

        static string Serialize<T>(T value) => JsonSerializer.Serialize(value);

        static T Deserialize<T>(string value) => JsonSerializer.Deserialize<T>(value);

        public void SetOperation(UnicastTransportOperation operation)
        {
            Destination = operation.Destination;
            Body = operation.Message.Body.ToArray();
            MessageId = operation.Message.MessageId;
            Headers = Serialize(operation.Message.Headers);
        }

        public UnicastTransportOperation GetOperation() =>
            new(new OutgoingMessage(MessageId, Deserialize<Dictionary<string, string>>(Headers), Body), Destination, []);

        public string PartitionKey { get; set; }

        public string RowKey { get; set; }

        public DateTimeOffset? Timestamp { get; set; }

        public ETag ETag { get; set; }

        const string PartitionKeyScope = "yyyyMMddHH";
        const string RowKeyScope = "yyyyMMddHHmmss";

        public static string GetPartitionKey(DateTimeOffset dateTimeOffset) => dateTimeOffset.ToString(PartitionKeyScope);

        public static string GetRawRowKeyPrefix(DateTimeOffset dateTimeOffset) => dateTimeOffset.ToString(RowKeyScope);
    }
}