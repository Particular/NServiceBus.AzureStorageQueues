using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using NServiceBus;
using NServiceBus.AcceptanceTesting.Support;

class ConfigureAzureStorageQueueTransport : IConfigureTestExecution
{
    string connectionString;

    public Task Configure(BusConfiguration configuration, IDictionary<string, string> settings)
    {
        connectionString = settings["Transport.ConnectionString"];
        configuration.UseTransport<AzureStorageQueueTransport>().ConnectionString(connectionString);
        return Task.FromResult(0);
    }

    public async Task Cleanup()
    {
        var storage = CloudStorageAccount.Parse(connectionString);
        var queues = storage.CreateCloudQueueClient();
        foreach (var queue in queues.ListQueues())
        {
            await queue.DeleteIfExistsAsync();
        }
    }
}