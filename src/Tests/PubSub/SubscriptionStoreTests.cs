namespace NServiceBus.Transport.AzureStorageQueues.Tests.PubSub
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using global::Azure.Storage.Queues;
    using Microsoft.Azure.Cosmos.Table;
    using NUnit.Framework;
    using Testing;

    [TestFixture]
    public class SubscriptionStoreTests
    {
        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            queueServiceClient = new QueueServiceClient(Utilities.GetEnvConfiguredConnectionString());

            var account = CloudStorageAccount.Parse(Utilities.GetEnvConfiguredConnectionString());
            cloudTableClient = account.CreateCloudTableClient();
        }

        [SetUp]
        public void SetUp()
        {
            table = cloudTableClient.GetTableReference($"atable{Guid.NewGuid():N}");
            table.CreateIfNotExists();
        }

        [TearDown]
        public void TearDown() => table.Delete();


        [Test]
        public async Task Subscribe_should_create_topics()
        {
            var settings = new AzureStorageAddressingSettings(new QueueAddressGenerator(s => s), "default", table.Name, new Dictionary<string, AccountInfo>(), new AccountInfo("", queueServiceClient, cloudTableClient));

            var subscriptionStore = new SubscriptionStore(settings);

            await subscriptionStore.Subscribe("endpointName", "localaddress", typeof(MyOtherEvent));
            await subscriptionStore.Subscribe("endpointName", "localaddress", typeof(MyOtherUnrelatedEvent));

            var query = new TableQuery<DynamicTableEntity>().Where(TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.Equal, "endpointName"));
            var entities = (await table.QueryUpTo(query, maxItemsToReturn: 100)).ToArray();
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
        public async Task Subscribe_with_mapped_events_should_create_topics()
        {
            var settings = new AzureStorageAddressingSettings(new QueueAddressGenerator(s => s), "subscriber",
                table.Name, new Dictionary<string, AccountInfo>(),
                new AccountInfo("", queueServiceClient, cloudTableClient));

            var publisherAccount = new AccountInfo("publisher", queueServiceClient, cloudTableClient);
            publisherAccount.AddEndpoint("publisherEndpoint", new[] { typeof(MyEventPublishedOnAnotherAccount) }, subscriptionTableName: table.Name);
            settings.Add(publisherAccount);

            var subscriptionStore = new SubscriptionStore(settings);

            await subscriptionStore.Subscribe("subscriberEndpoint", "subscriberAddress", typeof(MyOtherEvent));
            await subscriptionStore.Subscribe("subscriberEndpoint", "subscriberAddress", typeof(MyOtherUnrelatedEvent));
            await subscriptionStore.Subscribe("subscriberEndpoint", "subscriberAddress", typeof(MyEventPublishedOnAnotherAccount));

            var query = new TableQuery<DynamicTableEntity>().Where(TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.Equal, "subscriberEndpoint"));
            var entities = (await table.QueryUpTo(query, maxItemsToReturn: 100)).ToArray();

            CollectionAssert.AreEqual(new[]
            {
                typeof(MyEventPublishedOnAnotherAccount).FullName,
                typeof(MyOtherEvent).FullName,
                typeof(MyOtherUnrelatedEvent).FullName
            }, entities.Select(x => x.PartitionKey).ToList());

            CollectionAssert.AreEqual(new[]
            {
                "subscriberAddress@subscriber",
                "subscriberAddress",
                "subscriberAddress"
            }, entities.Select(x => x["Address"].StringValue).ToList());
        }

        [Test]
        public async Task Unsubscribe_should_delete_topics()
        {
            var settings = new AzureStorageAddressingSettings(new QueueAddressGenerator(s => s), "default", table.Name, new Dictionary<string, AccountInfo>(), new AccountInfo("", queueServiceClient, cloudTableClient));

            var subscriptionStore = new SubscriptionStore(settings);

            await subscriptionStore.Subscribe("endpointName", "localaddress", typeof(MyOtherEvent));
            await subscriptionStore.Subscribe("endpointName", "localaddress", typeof(MyOtherUnrelatedEvent));

            await subscriptionStore.Unsubscribe("endpointName", typeof(MyOtherEvent));

            var query = new TableQuery<TableEntity>().Where(TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.Equal, "endpointName"));
            var entities = (await table.QueryUpTo(query, maxItemsToReturn: 100)).ToArray();
            var topics = entities.Select(x => x.PartitionKey).ToList();

            CollectionAssert.AreEqual(new[]
            {
                typeof(MyOtherUnrelatedEvent).FullName
            }, topics);
        }

        [Test]
        public async Task Unsubscribe_with_mapped_events_should_delete_topics()
        {
            var settings = new AzureStorageAddressingSettings(new QueueAddressGenerator(s => s), "subscriber",
                table.Name, new Dictionary<string, AccountInfo>(),
                new AccountInfo("", queueServiceClient, cloudTableClient));

            var publisherAccount = new AccountInfo("publisher", queueServiceClient, cloudTableClient);
            publisherAccount.AddEndpoint("publisherEndpoint", new[] { typeof(MyEventPublishedOnAnotherAccount) }, subscriptionTableName: table.Name);
            settings.Add(publisherAccount);

            var subscriptionStore = new SubscriptionStore(settings);

            await subscriptionStore.Subscribe("subscriberEndpoint", "subscriberAddress", typeof(MyOtherEvent));
            await subscriptionStore.Subscribe("subscriberEndpoint", "subscriberAddress", typeof(MyOtherUnrelatedEvent));
            await subscriptionStore.Subscribe("subscriberEndpoint", "subscriberAddress", typeof(MyEventPublishedOnAnotherAccount));

            await subscriptionStore.Unsubscribe("subscriberEndpoint", typeof(MyEventPublishedOnAnotherAccount));

            var query = new TableQuery<DynamicTableEntity>().Where(TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.Equal, "subscriberEndpoint"));
            var entities = (await table.QueryUpTo(query, maxItemsToReturn: 100)).ToArray();

            CollectionAssert.AreEqual(new[]
            {
                typeof(MyOtherEvent).FullName,
                typeof(MyOtherUnrelatedEvent).FullName,
            }, entities.Select(x => x.PartitionKey).ToList());

            CollectionAssert.AreEqual(new[]
            {
                "subscriberAddress",
                "subscriberAddress"
            }, entities.Select(x => x["Address"].StringValue).ToList());
        }

        [Test]
        public async Task GetSubscribers()
        {
            var settings = new AzureStorageAddressingSettings(new QueueAddressGenerator(s => s), "default", table.Name, new Dictionary<string, AccountInfo>(), new AccountInfo("", queueServiceClient, cloudTableClient));

            var subscriptionStore = new SubscriptionStore(settings);

            await subscriptionStore.Subscribe("subscriberEndpoint", "subscriberAddress", typeof(MyOtherEvent));
            await subscriptionStore.Subscribe("subscriberEndpoint", "subscriberAddress", typeof(MyOtherUnrelatedEvent));

            var subcribers =
                await subscriptionStore.GetSubscribers(typeof(MyOtherEvent));

            CollectionAssert.AreEqual(new[] { "subscriberAddress" }, subcribers);
        }

        [Test]
        public async Task GetSubscribers_supports_polymorphism()
        {
            var settings = new AzureStorageAddressingSettings(new QueueAddressGenerator(s => s), "default", table.Name, new Dictionary<string, AccountInfo>(), new AccountInfo("", queueServiceClient, cloudTableClient));

            var subscriptionStore = new SubscriptionStore(settings);

            await subscriptionStore.Subscribe("subscriberEndpoint", "subscriberAddress", typeof(MyEvent));

            var subcribers =
                await subscriptionStore.GetSubscribers(typeof(MyOtherEvent));

            CollectionAssert.AreEqual(new[] { "subscriberAddress" }, subcribers);
        }

        [Test]
        public async Task GetSubscribers_with_mapped_events()
        {
            var settings = new AzureStorageAddressingSettings(new QueueAddressGenerator(s => s), "subscriber",
                table.Name, new Dictionary<string, AccountInfo>(),
                new AccountInfo("", queueServiceClient, cloudTableClient));

            var publisherAccount = new AccountInfo("publisher", queueServiceClient, cloudTableClient);
            publisherAccount.AddEndpoint("publisherEndpoint", new[] { typeof(MyEventPublishedOnAnotherAccount) }, subscriptionTableName: table.Name);
            settings.Add(publisherAccount);

            var subscriptionStore = new SubscriptionStore(settings);

            await subscriptionStore.Subscribe("subscriberEndpoint", "subscriberAddress", typeof(MyOtherEvent));
            await subscriptionStore.Subscribe("subscriberEndpoint", "subscriberAddress", typeof(MyOtherUnrelatedEvent));
            await subscriptionStore.Subscribe("subscriberEndpoint", "subscriberAddress", typeof(MyEventPublishedOnAnotherAccount));

            var subcribers =
                await subscriptionStore.GetSubscribers(typeof(MyEventPublishedOnAnotherAccount));

            CollectionAssert.AreEqual(new[] { "subscriberAddress@subscriber" }, subcribers);
        }

        [Test]
        public async Task GetSubscribers_with_mapped_events_supports_polymorphism()
        {
            var settings = new AzureStorageAddressingSettings(new QueueAddressGenerator(s => s), "subscriber",
                table.Name, new Dictionary<string, AccountInfo>(),
                new AccountInfo("", queueServiceClient, cloudTableClient));

            var publisherAccount = new AccountInfo("publisher", queueServiceClient, cloudTableClient);
            publisherAccount.AddEndpoint("publisherEndpoint", new[] { typeof(MyEvent) }, subscriptionTableName: table.Name);
            settings.Add(publisherAccount);

            var subscriptionStore = new SubscriptionStore(settings);

            await subscriptionStore.Subscribe("subscriberEndpoint", "subscriberAddress", typeof(MyEvent));

            var subcribers =
                await subscriptionStore.GetSubscribers(typeof(MyOtherEvent));

            CollectionAssert.AreEqual(new[] { "subscriberAddress@subscriber" }, subcribers);
        }

        [Test]
        public void Type_hierarchy_should_include_object()
        {
            var types = SubscriptionStore.GenerateTopics(typeof(MyOtherEvent));

            Assert.That(types, Has.One.EqualTo(typeof(object).FullName));
        }

        QueueServiceClient queueServiceClient;
        CloudTableClient cloudTableClient;
        CloudTable table;

        class MyEvent : IEvent
        {
        }

        class MyOtherEvent : MyEvent
        {

        }

        class MyOtherUnrelatedEvent : IEvent
        {
        }

        class MyEventPublishedOnAnotherAccount : IEvent
        {

        }
    }
}