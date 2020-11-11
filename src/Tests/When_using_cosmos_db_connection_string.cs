using System;
using NServiceBus.Settings;
using NServiceBus.Transport.AzureStorageQueues;
using NUnit.Framework;

namespace NServiceBus.Transport.AzureStorageQueues.Tests
{
    [TestFixture]
    public class When_using_cosmos_db_connection_string
    {
        [Test]
        public void Should_throw()
        {
            var connectionString = "DefaultEndpointsProtocol=https;AccountName=abcdefg;AccountKey=kIHCqw2jaATPAaayJoiJwPEYZITu2Ic5p4rDyJ5OhU97aub4IzoNCuTbuWgeaY29zQcw==;TableEndpoint=https://abcdefg.table.cosmos.azure.com:443/;";

            Assert.Throws<Exception>(() => new AzureStorageQueueInfrastructure(new SettingsHolder(), connectionString));
        }
    }
}