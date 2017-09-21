using System.Linq;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using NUnit.Framework;
using System;

[TestFixture]
[Category("Azure")]
public class Connect_to_Azure
{
    [Test]
    public async Task Should_parse_namespace_from_azure_storage_connectionstring()
    {
        Console.WriteLine("    Connection String :: " + Utils.GetEnvConfiguredConnectionString());
        var connectionString = Utils.GetEnvConfiguredConnectionString().Replace("\\;", ";").Replace("'", "");
        Console.WriteLine("    Formatted Connection String :: " + connectionString);
        var cloudStorageAccount = CloudStorageAccount.Parse(connectionString);
        var queueClient = cloudStorageAccount.CreateCloudQueueClient();
        var result = await queueClient.ListQueuesSegmentedAsync(new QueueContinuationToken());
        Assert.True(result.Results.Any());
    }
}
