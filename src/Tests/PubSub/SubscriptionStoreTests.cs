namespace NServiceBus.Transport.AzureStorageQueues.Tests.PubSub
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Table;
    using NUnit.Framework;
    using Testing;

    [TestFixture]
    public class SubscriptionStoreTests
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
        public async Task SubscribeAll_should_create_topics()
        {
            var subscriptionStore = new SubscriptionStore(table);

            await subscriptionStore.Subscribe("endpointName", "localaddress", typeof(MyOtherEvent), CancellationToken.None);
            await subscriptionStore.Subscribe("endpointName", "localaddress", typeof(MyOtherUnrelatedEvent), CancellationToken.None);

            var query = new TableQuery<DynamicTableEntity>().Where(TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.Equal, "endpointName"));
            var entities = (await table.ExecuteQueryAsync(query, take: 100, CancellationToken.None)).ToArray();
            var topics = entities.Select(x => x.PartitionKey).ToList();

            CollectionAssert.AreEqual(new[]
            {
                typeof(MyOtherEvent).FullName,
                typeof(MyOtherUnrelatedEvent).FullName,
            }, topics);

            Assert.True(entities.All(e => e.RowKey == "endpointName"), "The row key must match the endpoint name");
            Assert.True(entities.All(e => e["Address"].StringValue == "localaddress"), "The address must match the local address");
        }

        [Test]
        public async Task Unsubscribe_should_delete_topics()
        {
            var subscriptionStore = new SubscriptionStore(table);

            await subscriptionStore.Subscribe("endpointName", "localaddress", typeof(MyOtherEvent), CancellationToken.None);
            await subscriptionStore.Subscribe("endpointName", "localaddress", typeof(MyOtherUnrelatedEvent), CancellationToken.None);

            await subscriptionStore.Unsubscribe("endpointName", typeof(MyOtherEvent), CancellationToken.None);

            var query = new TableQuery<TableEntity>().Where(TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.Equal, "endpointName"));
            var entities = (await table.ExecuteQueryAsync(query, take: 100, CancellationToken.None)).ToArray();
            var topics = entities.Select(x => x.PartitionKey).ToList();

            CollectionAssert.AreEqual(new[]
            {
                typeof(MyOtherUnrelatedEvent).FullName
            }, topics);
        }

        [Test]
        public async Task GetSubscribers()
        {
            var subscriptionStore = new SubscriptionStore(table);

            await subscriptionStore.Subscribe("endpointName", "localaddress", typeof(MyOtherEvent), CancellationToken.None);
            await subscriptionStore.Subscribe("endpointName", "localaddress", typeof(MyOtherUnrelatedEvent), CancellationToken.None);

            var subcribers =
                await subscriptionStore.GetSubscribers(typeof(MyOtherEvent), CancellationToken.None);

            CollectionAssert.AreEqual(new[] { "localaddress" }, subcribers);
        }

        [Test]
        public void Type_hierarchy_should_include_object()
        {
            var types = SubscriptionStore.GenerateTopics(typeof(MyOtherEvent));

            Assert.That(types, Has.One.EqualTo(typeof(object).FullName));
        }

        class MyEvent : IEvent
        {
        }

        class MyOtherEvent : MyEvent
        {

        }

        class MyOtherUnrelatedEvent : IEvent
        {
        }
    }
}