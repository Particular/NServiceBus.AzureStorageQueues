namespace NServiceBus.AzureStorageQueues.Tests.Configuration
{
    using System;
    using System.Collections.Generic;
    using NServiceBus.Azure.Transports.WindowsAzureStorageQueues.Config;
    using NServiceBus.AzureStorageQueue.Addressing;
    using NServiceBus.Settings;
    using NUnit.Framework;

    [TestFixture]
    [Category("Azure")]
    public class When_configuring_account_partitioning
    {
        [Test]
        public void Should_be_able_to_set_the_partitioning_strategy()
        {
            var settings = new SettingsHolder();
            var extensions = new TransportExtensions<AzureStorageQueueTransport>(settings);

            extensions.Partitioning().UseStrategy<MyAccountPartitioningStrategy>();

            Assert.AreEqual(typeof(MyAccountPartitioningStrategy), settings.Get<Type>(WellKnownConfigurationKeys.Addressing.Partitioning.Strategy));
        }

        class MyAccountPartitioningStrategy : IAccountPartitioningStrategy
        {
            public IEnumerable<string> GetAccounts(PartitioningIntent partitioningIntent)
            {
                throw new NotImplementedException();
            }
        }
    }
}