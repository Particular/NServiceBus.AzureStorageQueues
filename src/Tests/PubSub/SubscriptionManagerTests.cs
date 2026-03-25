namespace NServiceBus.Transport.AzureStorageQueues.Tests.PubSub
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Extensibility;
    using NUnit.Framework;
    using Unicast.Messages;

    [TestFixture]
    public class SubscriptionManagerTests
    {
        [Test]
        public async Task Should_subscribe_all_event_types()
        {
            var subscriptionStore = new SubscriptionStore();
            var subscriptionManager = new SubscriptionManager(subscriptionStore, "MyEndpoint", "myendpoint", new StartupDiagnosticEntries());

            await subscriptionManager.SubscribeAll(
                [new MessageMetadata(typeof(MyEvent1)), new MessageMetadata(typeof(MyEvent2))],
                new ContextBag());

            Assert.That(subscriptionStore.SubscribedTypes, Is.EquivalentTo(new[] { typeof(MyEvent1), typeof(MyEvent2) }));
        }

        [Test]
        public async Task Should_succeed_with_no_events()
        {
            var subscriptionStore = new SubscriptionStore();
            var subscriptionManager = new SubscriptionManager(subscriptionStore, "MyEndpoint", "myendpoint", new StartupDiagnosticEntries());

            await subscriptionManager.SubscribeAll([], new ContextBag());

            Assert.That(subscriptionStore.SubscribedTypes, Is.Empty);
        }

        class MyEvent1;

        class MyEvent2;

        class SubscriptionStore : ISubscriptionStore
        {
            public List<Type> SubscribedTypes { get; } = [];

            public Task<IEnumerable<string>> GetSubscribers(Type eventType, CancellationToken cancellationToken = default) =>
                Task.FromResult(Enumerable.Empty<string>());

            public Task Subscribe(string endpointName, string endpointAddress, Type eventType, CancellationToken cancellationToken = default)
            {
                SubscribedTypes.Add(eventType);
                return Task.CompletedTask;
            }

            public Task Unsubscribe(string endpointName, Type eventType, CancellationToken cancellationToken = default) =>
                Task.CompletedTask;
        }
    }
}