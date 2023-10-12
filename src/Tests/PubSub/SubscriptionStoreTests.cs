namespace NServiceBus.Transport.AzureStorageQueues.Tests.PubSub
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using global::Azure.Data.Tables;
    using global::Azure.Storage.Queues;
    using NUnit.Framework;
    using Testing;

    [TestFixture]
    public class SubscriptionStoreTests
    {
        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            queueServiceClient = new QueueServiceClient(Utilities.GetEnvConfiguredConnectionString());
            tableServiceClient = new TableServiceClient(Utilities.GetEnvConfiguredConnectionString());
        }

        [SetUp]
        public async Task SetUp()
        {
            tableClient = tableServiceClient.GetTableClient($"atable{Guid.NewGuid():N}");
            var response = await tableClient.CreateIfNotExistsAsync();
            Assert.That(response.Value, Is.Not.Null);
        }

        [TearDown]
        public async Task TearDown()
        {
            var response = await tableClient.DeleteAsync();
            Assert.That(response.IsError, Is.False);
        }

        [Test]
        public async Task Subscribe_should_create_topics()
        {
            var settings = new AzureStorageAddressingSettings(new QueueAddressGenerator(s => s), "default", tableClient.Name, [], new AccountInfo("", queueServiceClient, tableServiceClient));

            var subscriptionStore = new SubscriptionStore(settings);

            await subscriptionStore.Subscribe("endpointName", "localaddress", typeof(MyOtherEvent));
            await subscriptionStore.Subscribe("endpointName", "localaddress", typeof(MyOtherUnrelatedEvent));


            var entities = await tableClient.QueryAsync<TableEntity>(e => e.RowKey.Equals("endpointName"))
                .Take(100)
                .ToListAsync()
                .ConfigureAwait(false);
            var topics = entities.Select(x => x.PartitionKey).ToList();

            CollectionAssert.AreEqual(new[]
            {
                typeof(MyOtherEvent).FullName,
                typeof(MyOtherUnrelatedEvent).FullName,
            }, topics);

            Assert.True(entities.All(e => e.RowKey == "endpointName"), "The row key must match the endpoint name");
            Assert.True(entities.All(e => e["Address"].ToString() == "localaddress"), "The address must match the local address");
        }

        [Test]
        public async Task Subscribe_with_mapped_events_should_create_topics()
        {
            var settings = new AzureStorageAddressingSettings(new QueueAddressGenerator(s => s), "subscriber",
                tableClient.Name, [],
                new AccountInfo("", queueServiceClient, tableServiceClient));

            var publisherAccount = new AccountInfo("publisher", queueServiceClient, tableServiceClient);
            publisherAccount.AddEndpoint("publisherEndpoint", new[] { typeof(MyEventPublishedOnAnotherAccount) }, subscriptionTableName: tableClient.Name);
            settings.Add(publisherAccount);

            var subscriptionStore = new SubscriptionStore(settings);

            await subscriptionStore.Subscribe("subscriberEndpoint", "subscriberAddress", typeof(MyOtherEvent));
            await subscriptionStore.Subscribe("subscriberEndpoint", "subscriberAddress", typeof(MyOtherUnrelatedEvent));
            await subscriptionStore.Subscribe("subscriberEndpoint", "subscriberAddress", typeof(MyEventPublishedOnAnotherAccount));

            var entities = await tableClient.QueryAsync<TableEntity>(e => e.RowKey.Equals("subscriberEndpoint"))
                .Take(100)
                .ToListAsync()
                .ConfigureAwait(false);

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
            }, entities.Select(x => x["Address"].ToString()).ToList());
        }

        [Test]
        public async Task Unsubscribe_should_delete_topics()
        {
            var settings = new AzureStorageAddressingSettings(new QueueAddressGenerator(s => s), "default", tableClient.Name, [], new AccountInfo("", queueServiceClient, tableServiceClient));

            var subscriptionStore = new SubscriptionStore(settings);

            await subscriptionStore.Subscribe("endpointName", "localaddress", typeof(MyOtherEvent));
            await subscriptionStore.Subscribe("endpointName", "localaddress", typeof(MyOtherUnrelatedEvent));

            await subscriptionStore.Unsubscribe("endpointName", typeof(MyOtherEvent));

            var topics = await tableClient.QueryAsync<TableEntity>(e => e.RowKey.Equals("endpointName"))
                .Take(100)
                .Select(x => x.PartitionKey)
                .ToListAsync()
                .ConfigureAwait(false);

            CollectionAssert.AreEqual(new[] { typeof(MyOtherUnrelatedEvent).FullName }, topics);
        }

        [Test]
        public async Task Unsubscribe_with_mapped_events_should_delete_topics()
        {
            var settings = new AzureStorageAddressingSettings(new QueueAddressGenerator(s => s), "subscriber",
                tableClient.Name, [],
                new AccountInfo("", queueServiceClient, tableServiceClient));

            var publisherAccount = new AccountInfo("publisher", queueServiceClient, tableServiceClient);
            publisherAccount.AddEndpoint("publisherEndpoint", new[] { typeof(MyEventPublishedOnAnotherAccount) }, subscriptionTableName: tableClient.Name);
            settings.Add(publisherAccount);

            var subscriptionStore = new SubscriptionStore(settings);

            await subscriptionStore.Subscribe("subscriberEndpoint", "subscriberAddress", typeof(MyOtherEvent));
            await subscriptionStore.Subscribe("subscriberEndpoint", "subscriberAddress", typeof(MyOtherUnrelatedEvent));
            await subscriptionStore.Subscribe("subscriberEndpoint", "subscriberAddress", typeof(MyEventPublishedOnAnotherAccount));

            await subscriptionStore.Unsubscribe("subscriberEndpoint", typeof(MyEventPublishedOnAnotherAccount));

            var entities = await tableClient.QueryAsync<TableEntity>(e => e.RowKey.Equals("subscriberEndpoint"))
                .Take(100)
                .ToListAsync()
                .ConfigureAwait(false);

            CollectionAssert.AreEqual(new[]
            {
                typeof(MyOtherEvent).FullName,
                typeof(MyOtherUnrelatedEvent).FullName,
            }, entities.Select(x => x.PartitionKey).ToList());

            CollectionAssert.AreEqual(new[]
            {
                "subscriberAddress",
                "subscriberAddress"
            }, entities.Select(x => x["Address"].ToString()).ToList());
        }

        [Test]
        public async Task GetSubscribers()
        {
            var settings = new AzureStorageAddressingSettings(new QueueAddressGenerator(s => s), "default", tableClient.Name, [], new AccountInfo("", queueServiceClient, tableServiceClient));

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
            var settings = new AzureStorageAddressingSettings(new QueueAddressGenerator(s => s), "default", tableClient.Name, [], new AccountInfo("", queueServiceClient, tableServiceClient));

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
                tableClient.Name, [],
                new AccountInfo("", queueServiceClient, tableServiceClient));

            var publisherAccount = new AccountInfo("publisher", queueServiceClient, tableServiceClient);
            publisherAccount.AddEndpoint("publisherEndpoint", new[] { typeof(MyEventPublishedOnAnotherAccount) }, subscriptionTableName: tableClient.Name);
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
                tableClient.Name, [],
                new AccountInfo("", queueServiceClient, tableServiceClient));

            var publisherAccount = new AccountInfo("publisher", queueServiceClient, tableServiceClient);
            publisherAccount.AddEndpoint("publisherEndpoint", new[] { typeof(MyEvent) }, subscriptionTableName: tableClient.Name);
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
        TableServiceClient tableServiceClient;
        TableClient tableClient;

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