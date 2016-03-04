using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using NServiceBus;
using NServiceBus.AcceptanceTesting.Support;
using NServiceBus.Azure.Transports.WindowsAzureStorageQueues;
using NServiceBus.Configuration.AdvanceExtensibility;
using NServiceBus.Transports;

class ConfigureAzureStorageQueueTransport : IConfigureTestExecution
{
    BusConfiguration busConfiguration;
    string connectionString;

    public Task Configure(BusConfiguration configuration, IDictionary<string, string> settings)
    {
        connectionString = settings["Transport.ConnectionString"];
        configuration.UseSerialization<JsonSerializer>();
        configuration.UseTransport<AzureStorageQueueTransport>()
            .ConnectionString(connectionString)
            .MessageInvisibleTime(TimeSpan.FromSeconds(5))
            .SerializeMessageWrapperWith(defintion => MessageWrapperSerializer.Json.Value);

        busConfiguration = configuration;

        return Task.FromResult(0);
    }

    public async Task Cleanup()
    {
        var storage = CloudStorageAccount.Parse(connectionString);
        var queues = storage.CreateCloudQueueClient();

        var queuesNames = GetTestRelatedQueueNames();

        foreach (var queuesName in queuesNames)
        {
            var queue = queues.GetQueueReference(queuesName);
            if (await queue.ExistsAsync())
            {
                await queue.ClearAsync();
            }
        }
    }

    private IEnumerable<string> GetTestRelatedQueueNames()
    {
        var bindings = busConfiguration.GetSettings().Get<QueueBindings>();
        var generator = new QueueAddressGenerator(busConfiguration.GetSettings());
        return bindings.ReceivingAddresses.Concat(bindings.SendingAddresses).Select(queue => generator.GetQueueName(queue));
    }
}