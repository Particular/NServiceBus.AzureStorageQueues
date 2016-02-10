using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using NServiceBus;
using NServiceBus.AcceptanceTesting.Support;
using NServiceBus.Azure.Transports.WindowsAzureStorageQueues;

class ConfigureAzureStorageQueueTransport : IConfigureTestExecution
{
    string connectionString;

    public Task Configure(BusConfiguration configuration, IDictionary<string, string> settings)
    {
        connectionString = settings["Transport.ConnectionString"];
        configuration.UseSerialization<JsonSerializer>();
        configuration.UseTransport<AzureStorageQueueTransport>()
            .ConnectionString(connectionString)
            .MessageInvisibleTime(TimeSpan.FromSeconds(5))
            .SerializeMessageWrapperWith(defintion => MessageWrapperSerializer.Json.Value)
            .CreateSendingQueues();

        return Task.FromResult(0);
    }

    public async Task Cleanup()
    {
        var storage = CloudStorageAccount.Parse(connectionString);
        var queues = storage.CreateCloudQueueClient();
        var cloudQueues = new Queue<CloudQueue>(queues.ListQueues());

        while (cloudQueues.Count > 0)
        {
            const int batchSize = 32;
            var tasks = new List<Task>();
            for (var i = 0; i < batchSize; i++)
            {
                if (cloudQueues.Count > 0)
                {
                    var queue = cloudQueues.Dequeue();
                    tasks.Add(queue.ClearAsync());
                }
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
    }
}