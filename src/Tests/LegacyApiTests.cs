#pragma warning disable 0618
#pragma warning disable 0619
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

            Assert.AreEqual(expectedMessageInvisibleTime, transport.AsqTransport.MessageInvisibleTime);
            Assert.AreEqual(expectedPeekInterval, transport.AsqTransport.PeekInterval);
            Assert.AreEqual(expectedMaximumWaitTimeWhenIdle, transport.AsqTransport.MaximumWaitTimeWhenIdle);
            // Assert.AreEqual(expectedQueueNameSanitizer, transport.AsqTransport.QueueNameSanitizer);
            Assert.AreEqual(expectedBatchSize, transport.AsqTransport.ReceiverBatchSize);
            Assert.AreEqual(expectedDegreeOfReceiveParallelism, transport.AsqTransport.DegreeOfReceiveParallelism);
            Assert.AreEqual(typeof(XmlSerializer), transport.AsqTransport.MessageWrapperSerializationDefinition.GetType());
            Assert.AreEqual(expectedMessagesUnwrapper, transport.AsqTransport.MessageUnwrapper);
            Assert.AreEqual(expectedDelayedDeliveryTableName, transport.AsqTransport.DelayedDelivery.DelayedDeliveryTableName);
            Assert.AreEqual(expectedDefaultAccountAlias, transport.AsqTransport.AccountRouting.DefaultAccountAlias);
        }
    }
}
#pragma warning restore 0619
#pragma warning restore 0618