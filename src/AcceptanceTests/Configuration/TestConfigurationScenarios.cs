namespace NServiceBus.Transport.AzureStorageQueues.AcceptanceTests
{
    using System;
    using System.Collections;
    using System.Linq;
    using Configuration.AdvancedExtensibility;
    using Features;
    using global::Azure.Storage.Blobs;
    using global::Azure.Storage.Queues;
    using Microsoft.Azure.Cosmos.Table;
    using NServiceBus.AcceptanceTests;
    using NUnit.Framework;

    public class TestConfigurationScenarios : NServiceBusAcceptanceTest
    {
        [TestCaseSource(nameof(Scenarios))]
        public void Check_for_missing_configuration(
            bool disablePublish,
            bool disableDelayedDelivery,
            bool setConnectionString,
            bool setQueueServiceClient,
            bool setBlobServiceClient,
            bool setCloudTableClient)
        {
            var endpointConfiguration = new EndpointConfiguration("AnEndpoint");
            endpointConfiguration.UseSerialization<XmlSerializer>();
            endpointConfiguration.EnableInstallers();
            endpointConfiguration.DisableFeature<TimeoutManager>();
            endpointConfiguration.DisableFeature<Sagas>();
            endpointConfiguration.UsePersistence<TestingInMemoryPersistence>();
            var transport = endpointConfiguration.UseTransport<AzureStorageQueueTransport>();
            // NOTE: This doesn't disable native pub-sub. Only message-driven pub-sub
            transport.DisablePublishing();

            if (disablePublish)
            {
                // NOTE: There is no public API that sets this
                transport.GetSettings().Set(WellKnownConfigurationKeys.PubSub.DisablePublishSubscribe, true);
            }

            if (disableDelayedDelivery)
            {
                transport.DelayedDelivery().DisableDelayedDelivery();
            }

            if (setConnectionString)
            {
                transport.ConnectionString(ConfigureEndpointAzureStorageQueueTransport.AnotherConnectionString);
            }

            if (setQueueServiceClient)
            {
                transport.UseQueueServiceClient(new QueueServiceClient(ConfigureEndpointAzureStorageQueueTransport.AnotherConnectionString));
            }

            if (setBlobServiceClient)
            {
                transport.UseBlobServiceClient(new BlobServiceClient(ConfigureEndpointAzureStorageQueueTransport.AnotherConnectionString));
            }

            if (setCloudTableClient)
            {
                var storageAccount = CloudStorageAccount.Parse(ConfigureEndpointAzureStorageQueueTransport.AnotherConnectionString);

                transport.UseCloudTableClient(new CloudTableClient(storageAccount.TableStorageUri, storageAccount.Credentials));
            }

            var queueServiceClientNeeded = !setConnectionString;
            var blobServiceNeeded = !setConnectionString && !disableDelayedDelivery;
            var cloudTableNeeded = !setConnectionString && (!disableDelayedDelivery || !disablePublish);
            var shouldThrow = (queueServiceClientNeeded && !setQueueServiceClient)
                || (blobServiceNeeded && !setBlobServiceClient)
                || (cloudTableNeeded && !setCloudTableClient);

            var message = $@"Creating an endpoint {(shouldThrow ? "should" : "should not")} throw when:
- Fallback connection string {(setConnectionString ? "is" : "is not")} set
- Queue Service Client {(setQueueServiceClient ? "is" : "is not")} set
- Blob Service Client {(setBlobServiceClient ? "is" : "is not")} set
- Cloud Table Client {(setCloudTableClient ? "is" : "is not")} set
- Publishing is {(disablePublish ? "disabled" : "enabled")}
- Delayed delivery is {(disableDelayedDelivery ? "disabled" : "enabled")}";

            if (shouldThrow)
            {
                Assert.ThrowsAsync<Exception>(() => Endpoint.Create(endpointConfiguration), message);
            }
            else
            {
                Assert.DoesNotThrowAsync(() => Endpoint.Create(endpointConfiguration), message);
            }
        }

        public static IEnumerable Scenarios =>
            from disablePublish in new[] { true, false }
            from disableDelayedDelivery in new[] { true, false }
            from setConnectionString in new[] { true, false }
            from setQueueServiceClient in new[] { true, false }
            from setBlobServiceClient in new[] { true, false }
            from setCloudTableClient in new[] { true, false }
            select new TestCaseData(
                disablePublish,
                disableDelayedDelivery,
                setConnectionString,
                setQueueServiceClient,
                setBlobServiceClient,
                setCloudTableClient
            );
    }
}