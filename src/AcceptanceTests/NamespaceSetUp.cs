using Microsoft.WindowsAzure.Storage;
using NUnit.Framework;

[SetUpFixture]
public class NamespaceSetUp
{
    static string connectionString;

    [TearDown]
    public void TearDown()
    {
        var storage = CloudStorageAccount.Parse(connectionString);
        var queues = storage.CreateCloudQueueClient();
        foreach (var queue in queues.ListQueues())
        {
            queue.DeleteIfExists();
        }
    }

    public static void SetConnection(string conString)
    {
        if (connectionString == null)
        {
            connectionString = conString;
        }
    }
}