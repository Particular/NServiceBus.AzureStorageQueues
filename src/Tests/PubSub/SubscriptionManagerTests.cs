namespace NServiceBus.Transport.AzureStorageQueues.Tests.PubSub
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Extensibility;
    using Microsoft.Azure.Cosmos.Table;
    using NUnit.Framework;
    using Testing;
    using Unicast.Messages;

    [TestFixture]
    public class SubscriptionManagerTests
    {
        CloudTableClient client;
        CloudTable table;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            var account = CloudStorageAccount.Parse(Utilities.GetEnvConfiguredConnectionString());
            client = account.CreateCloudTableClient();
        }

        [SetUp]
        public void SetUp()
        {
            table = client.GetTableReference($"atable{Guid.NewGuid():N}");
            table.CreateIfNotExists();
        }

        [TearDown]
        public void TearDown() => table.Delete();


        [Test]
        public async Task Should_create_topics()
        {
            var subscriptionManager = new SubscriptionManager(table, "localaddress");

            var messagesMetadata = new[]
            {
                new MessageMetadata(typeof(MyOtherEvent), new[]
                {
                    typeof(IEvent),
                    typeof(MyEvent),
                    typeof(MyOtherEvent)
                })
            };

            await subscriptionManager.SubscribeAll(messagesMetadata, new ContextBag(), CancellationToken.None);

            var query = new TableQuery<TableEntity>().Where(TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.Equal, "localaddress"));
            var entities = (await table.ExecuteQueryAsync(query, take: 100, CancellationToken.None)).ToArray();
            var topics = entities.Select(x => x.PartitionKey).ToList();

            Assert.True("localaddress" == entities[0].RowKey);
            Assert.AreEqual(3, entities.Select(x => x.PartitionKey).Distinct().Count());
            Assert.AreEqual(3, entities.Length);
            Assert.Contains(messagesMetadata[0].MessageHierarchy[0].FullName, topics);
            Assert.Contains(messagesMetadata[0].MessageHierarchy[1].FullName, topics);
            Assert.Contains(messagesMetadata[0].MessageHierarchy[2].FullName, topics);
        }

        [Test]
        public async Task Should_delete_topics()
        {
            var subscriptionManager = new SubscriptionManager(table, "localaddress");

            var messageMetadata = new MessageMetadata(typeof(MyOtherEvent), new[]
            {
                typeof(IEvent),
                typeof(MyEvent),
                typeof(MyOtherEvent)
            });
            var messagesMetadata = new[]
            {
                messageMetadata
            };

            await subscriptionManager.SubscribeAll(messagesMetadata, new ContextBag(), CancellationToken.None);
            await subscriptionManager.Unsubscribe(messageMetadata, new ContextBag(), CancellationToken.None);

            var query = new TableQuery<TableEntity>().Where(TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.Equal, "localaddress"));
            var entities = (await table.ExecuteQueryAsync(query, take: 100, CancellationToken.None)).ToArray();

            Assert.AreEqual(0, entities.Length);
        }

        class MyEvent : IEvent
        {

        }

        class MyOtherEvent : MyEvent
        {

        }
    }
}