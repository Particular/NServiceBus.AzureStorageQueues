namespace NServiceBus.Azure.Transports.WindowsAzureStorageQueues.DelayDelivery
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Table;
    using Transport;

    class TimeoutEntity : TableEntity
    {
        public string Destination { get; set; }
        public byte[] Body { get; set; }
        public string MessageId { get; set; }
        public string Headers { get; set; }
        
        static string Serialize<T>(T value)
        {
            return SimpleJson.SimpleJson.SerializeObject(value);
        }

        static T Deserialize<T>(string value)
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
            return new UnicastTransportOperation(new OutgoingMessage(MessageId, Deserialize<Dictionary<string, string>>(Headers), Body), Destination);
        }

        const string PartitionKeyScope = "yyyyMMddHH";
        const string RowKeyScope = "yyyyMMddHHmmss";

        public static string GetPartitionKey(DateTimeOffset dto)
        {
            return dto.ToString(PartitionKeyScope);
        }

        public static string GetRawRowKeyPrefix(DateTimeOffset dto)
        {
            return dto.ToString(RowKeyScope);
        }

        public static Task<CloudTable> BuildTimeoutTableByQueueName(string connectionString, string queueName, CancellationToken cancellationToken)
        {
            var tableName = BuildTimeoutTableName(queueName);
            return BuiltTimeoutTableWithExplicitName(connectionString, tableName, cancellationToken);
        }

        public static async Task<CloudTable> BuiltTimeoutTableWithExplicitName(string connectionString, string tableName, CancellationToken cancellationToken)
        {
            CloudStorageAccount account;
            if (!CloudStorageAccount.TryParse(connectionString, out account))
            {
                throw new Exception($"Cannot parse ConnectionString to a CloudStorageAccount. ConnectionString: {connectionString}");
            }
            var tables = account.CreateCloudTableClient();
            var table = tables.GetTableReference(tableName);
            await table.CreateIfNotExistsAsync(cancellationToken).ConfigureAwait(false);
            return table;
        }

        static string BuildTimeoutTableName(string queueName)
        {
            return $"Timeouts{queueName.Replace("-", "")}";
        }

    }
}