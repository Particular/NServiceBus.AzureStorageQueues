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
        var cloudStorageAccount = CloudStorageAccount.Parse(Utils.GetEnvConfiguredConnectionString());
        var queueClient = cloudStorageAccount.CreateCloudQueueClient();
        var result = await queueClient.ListQueuesSegmentedAsync(new QueueContinuationToken());
        Assert.True(result.Results.Any());
    }
}
