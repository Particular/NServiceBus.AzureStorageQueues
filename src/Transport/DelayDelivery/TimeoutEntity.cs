namespace NServiceBus.Azure.Transports.WindowsAzureStorageQueues.DelayDelivery
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Table;
    using Newtonsoft.Json;
    using Transport;

    class TimeoutEntity : TableEntity
    {
        public string Destination { get; set; }
        public byte[] Body { get; set; }
        public string MessageId { get; set; }
        public string Headers { get; set; }
        
        static string Serialize<T>(T obj)
        {
            var sw = new StringWriter();
            new JsonSerializer().Serialize(sw, obj);
            sw.Flush();
            return sw.ToString();
        }

        static T Deserialize<T>(string obj)
        {
            return new JsonSerializer().Deserialize<T>(new JsonTextReader(new StringReader(obj)));
        }

        public void SetOperation(UnicastTransportOperation operation)
        {
            Destination = operation.Destination;
            Body = operation.Message.Body;
            MessageId=operation.Message.MessageId;
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
            var tables = CloudStorageAccount.TryParse(connectionString, out account) ? account.CreateCloudTableClient() : null;
            // TODO: fix the naming or add queue to the timeout
            var t = tables.GetTableReference(tableName);
            await t.CreateIfNotExistsAsync(cancellationToken).ConfigureAwait(false);
            return t;
        }

        static string BuildTimeoutTableName(string queueName)
        {
            return TablePrefix + queueName.Replace("-","");
        }

        const string TablePrefix = "Timeouts";
    }
}