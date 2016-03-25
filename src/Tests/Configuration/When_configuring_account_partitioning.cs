namespace NServiceBus.AzureStorageQueues.Tests.Configuration
{
    using System;
    using System.Collections.Generic;
    using NServiceBus.AzureStorageQueues;
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

            var configuredStrategy = settings.Get<Type>(WellKnownConfigurationKeys.Addressing.Partitioning.Strategy);
            Assert.AreEqual(typeof(MyAccountPartitioningStrategy), configuredStrategy);
        }

        [Test]
        public void Should_be_able_to_add_a_new_account()
        {
            var settings = new SettingsHolder();
            var extensions = new TransportExtensions<AzureStorageQueueTransport>(settings);

            extensions.Partitioning().AddStorageAccount("accountName", "connectionString");

            var configuredAccounts = settings.Get<Dictionary<string, string>>(WellKnownConfigurationKeys.Addressing.Partitioning.Accounts);
            var expectedAccount = new KeyValuePair<string, string>("accountName", "connectionString");
            CollectionAssert.Contains(configuredAccounts, expectedAccount);
        }

        [Test]
        public void Registering_the_same_account_name_twice_should_result_in_a_single_account()
        {
            var settings = new SettingsHolder();
            var extensions = new TransportExtensions<AzureStorageQueueTransport>(settings);

            extensions.Partitioning().AddStorageAccount("accountName", "connectionString1");
            extensions.Partitioning().AddStorageAccount("accountName", "connectionString2");

            var configuredAccounts = settings.Get<Dictionary<string, string>>(WellKnownConfigurationKeys.Addressing.Partitioning.Accounts);
            var expectedAccount = new KeyValuePair<string, string>("accountName", "connectionString2");
            CollectionAssert.Contains(configuredAccounts, expectedAccount);
        }

        [Test]
        public void Registering_the_same_connection_string_twice_should_result_in_a_single_account()
        {
            var settings = new SettingsHolder();
            var extensions = new TransportExtensions<AzureStorageQueueTransport>(settings);

            extensions.Partitioning().AddStorageAccount("accountName1", "connectionString");
            extensions.Partitioning().AddStorageAccount("accountName2", "connectionString");

            var configuredAccounts = settings.Get<Dictionary<string, string>>(WellKnownConfigurationKeys.Addressing.Partitioning.Accounts);
            var expectedAccount = new KeyValuePair<string, string>("accountName1", "connectionString");
            CollectionAssert.Contains(configuredAccounts, expectedAccount);
            var notExpectedAccount = new KeyValuePair<string, string>("accountName2", "connectionString");
            CollectionAssert.DoesNotContain(configuredAccounts, notExpectedAccount);
        }

        [Test]
        [TestCase(null, "connectionString", "Storage account name can't be null or empty")]
        [TestCase("", "connectionString", "Storage account name can't be null or empty")]
        [TestCase(" ", "connectionString", "Storage account name can't be null or empty")]
        [TestCase("accountName", null, "Storage account connection string can't be null or empty")]
        [TestCase("accountName", "", "Storage account connection string can't be null or empty")]
        [TestCase("accountName", " ", "Storage account connection string can't be null or empty")]

        public void Should_not_be_possible_to_add_invalid_account(string accountName, string connectionString, string errorMessage)
        {
            var settings = new SettingsHolder();
            var extensions = new TransportExtensions<AzureStorageQueueTransport>(settings);

            var exception = Assert.Throws<ArgumentException>(() => extensions.Partitioning().AddStorageAccount(accountName, connectionString));
            StringAssert.Contains(errorMessage, exception.Message);
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