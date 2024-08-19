namespace NServiceBus.Transport.AzureStorageQueues.Tests
{
    using System;
    using Azure.Transports.WindowsAzureStorageQueues;
    using global::Azure.Storage.Queues.Models;
    using NUnit.Framework;

    [TestFixture]
    public class LegacyApiTests
    {
        [Test]
        public void Legacy_api_shim_sets_corresponding_new_api_properties()
        {
            var expectedConnectionString = "UseDevelopmentStorage=true";
            var expectedMessageInvisibleTime = TimeSpan.FromSeconds(42);
            var expectedPeekInterval = TimeSpan.FromMilliseconds(42);
            var expectedMaximumWaitTimeWhenIdle = TimeSpan.FromSeconds(42);
            // Func<string, string> expectedQueueNameSanitizer = s => "42";
            var expectedBatchSize = 42 - 20;
            var expectedDegreeOfReceiveParallelism = 42;
            Func<QueueMessage, MessageWrapper> expectedMessagesUnwrapper = message => null;
            var expectedDelayedDeliveryTableName = "table42";
            var expectedDefaultAccountAlias = "alias42";

            var config = new EndpointConfiguration("MyEndpoint");
            var transport = config.UseTransport<AzureStorageQueueTransport>();

            transport.ConnectionString(expectedConnectionString);
            transport.MessageInvisibleTime(expectedMessageInvisibleTime);
            transport.PeekInterval(expectedPeekInterval);
            transport.MaximumWaitTimeWhenIdle(expectedMaximumWaitTimeWhenIdle);
            // Cannot test this the transport wraps the given delegate into another one to catch exceptions
            // transport.SanitizeQueueNamesWith(expectedQueueNameSanitizer);
            transport.BatchSize(expectedBatchSize);
            transport.DegreeOfReceiveParallelism(expectedDegreeOfReceiveParallelism);
            transport.SerializeMessageWrapperWith<XmlSerializer>();
            transport.UnwrapMessagesWith(expectedMessagesUnwrapper);
            transport.DelayedDelivery().UseTableName(expectedDelayedDeliveryTableName);
            transport.AccountRouting().DefaultAccountAlias = expectedDefaultAccountAlias;

            Assert.Multiple(() =>
            {
                Assert.That(transport.Transport.MessageInvisibleTime, Is.EqualTo(expectedMessageInvisibleTime));
                Assert.That(transport.Transport.PeekInterval, Is.EqualTo(expectedPeekInterval));
                Assert.That(transport.Transport.MaximumWaitTimeWhenIdle, Is.EqualTo(expectedMaximumWaitTimeWhenIdle));
                // Assert.AreEqual(expectedQueueNameSanitizer, transport.AsqTransport.QueueNameSanitizer);
                Assert.That(transport.Transport.ReceiverBatchSize, Is.EqualTo(expectedBatchSize));
                Assert.That(transport.Transport.DegreeOfReceiveParallelism, Is.EqualTo(expectedDegreeOfReceiveParallelism));
                Assert.That(transport.Transport.MessageWrapperSerializationDefinition.GetType(), Is.EqualTo(typeof(XmlSerializer)));
                Assert.That(transport.Transport.MessageUnwrapper, Is.EqualTo(expectedMessagesUnwrapper));
                Assert.That(transport.Transport.DelayedDelivery.DelayedDeliveryTableName, Is.EqualTo(expectedDelayedDeliveryTableName));
                Assert.That(transport.Transport.AccountRouting.DefaultAccountAlias, Is.EqualTo(expectedDefaultAccountAlias));
            });
        }
    }
}