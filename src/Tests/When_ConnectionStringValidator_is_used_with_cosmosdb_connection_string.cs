namespace NServiceBus.Transport.AzureStorageQueues.Tests
{
    using System;
    using NUnit.Framework;

    [TestFixture]
    public class When_ConnectionStringValidator_is_used_with_cosmosdb_connection_string
    {
        [Test]
        public void Should_throw()
        {
            var connectionString = "DefaultEndpointsProtocol=https;AccountName=abcdefg;AccountKey=kIHCqw2jaATPAaayJoiJwPEYZITu2Ic5p4rDyJ5OhU97aub4IzoNCuTbuWgeaY29zQcw==;TableEndpoint=https://abcdefg.table.cosmos.azure.com:443/;";

            Assert.Throws<Exception>(() => ConnectionStringValidator.ThrowIfPremiumEndpointConnectionString(connectionString));
        }
    }
}