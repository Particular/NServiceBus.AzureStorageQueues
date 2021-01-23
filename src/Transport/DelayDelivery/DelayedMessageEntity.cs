﻿using System;
using System.Collections.Generic;
using Microsoft.Azure.Cosmos.Table;

namespace NServiceBus.Transport.AzureStorageQueues
{
    /// <summary>
    /// Represents a record in the native delays storage table which can be deferred message, saga timeouts, and delayed retries.
    /// </summary>
    internal class DelayedMessageEntity : TableEntity
    {
        public string Destination { get; set; }
        public byte[] Body { get; set; }
        public string MessageId { get; set; }
        public string Headers { get; set; }

        private static string Serialize<T>(T value)
        {
            return SimpleJson.SimpleJson.SerializeObject(value);
        }

        private static T Deserialize<T>(string value)
        {
            return SimpleJson.SimpleJson.DeserializeObject<T>(value);
        }

        public void SetOperation(UnicastTransportOperation operation)
        {
            Destination = operation.Destination;
            Body = operation.Message.Body;
            MessageId = operation.Message.MessageId;
            Headers = Serialize(operation.Message.Headers);
        }

        public UnicastTransportOperation GetOperation()
        {
            //TODO what about DispatchConsistency?
            return new UnicastTransportOperation(new OutgoingMessage(MessageId, Deserialize<Dictionary<string, string>>(Headers), Body), Destination, new OperationProperties());
        }

        private const string PartitionKeyScope = "yyyyMMddHH";
        private const string RowKeyScope = "yyyyMMddHHmmss";

        public static string GetPartitionKey(DateTimeOffset dto)
        {
            return dto.ToString(PartitionKeyScope);
        }

        public static string GetRawRowKeyPrefix(DateTimeOffset dto)
        {
            return dto.ToString(RowKeyScope);
        }
    }
}