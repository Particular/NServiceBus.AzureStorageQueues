using Microsoft.WindowsAzure.Storage;
using NUnit.Framework;

[SetUpFixture]
public class NamespaceSetUp
{
    public static string ConnectionString { get; set; }

    [TearDown]
    public async void TearDown()
    {
        var storage = CloudStorageAccount.Parse(ConnectionString);
        var queues = storage.CreateCloudQueueClient();
        foreach (var queue in queues.ListQueues())
        {
            await queue.DeleteIfExistsAsync().ConfigureAwait(false);
        }
    }
}