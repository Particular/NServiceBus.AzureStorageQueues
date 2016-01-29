using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using NServiceBus;
using NServiceBus.AcceptanceTesting.Support;
using NUnit.Framework;

class ConfigureAzureStorageQueueTransport : IConfigureTestExecution
{
    string connectionString;

    public Task Configure(BusConfiguration configuration, IDictionary<string, string> settings)
    {
        NamespaceSetUp.SetConnection(connectionString);
        connectionString = settings["Transport.ConnectionString"];
        configuration.UseTransport<AzureStorageQueueTransport>().ConnectionString(connectionString);
        return Task.FromResult(0);
    }

    public async Task Cleanup()
    {
        await Task.FromResult(0);
    }
}

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

    public static void SetConnection(string connectionString)
    {
        if (NamespaceSetUp.connectionString == null)
        {
            NamespaceSetUp.connectionString = connectionString;
        }
    }
}