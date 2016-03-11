using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using NServiceBus;
using NServiceBus.AcceptanceTesting.Support;
using NServiceBus.AcceptanceTests.ScenarioDescriptors;
using NServiceBus.Azure.Transports.WindowsAzureStorageQueues;
using NServiceBus.Configuration.AdvanceExtensibility;
using NServiceBus.Transports;

public class ConfigureScenariosForAzureStorageQueueTransport : IConfigureSupportedScenariosForTestExecution
{
    public IEnumerable<Type> UnsupportedScenarioDescriptorTypes { get; } = new[]
    {
        typeof(AllTransportsWithCentralizedPubSubSupport),
        typeof(AllDtcTransports),
        typeof(AllTransportsWithoutNativeDeferralAndWithAtomicSendAndReceive)
    };
}

public class ConfigureEndpointAzureStorageQueueTransport : IConfigureEndpointTestExecution
{
    private EndpointConfiguration endpointConfiguration;
    string connectionString;

    public Task Configure(BusConfiguration configuration, IDictionary<string, string> settings)
    {
        connectionString = settings.Get<string>("Transport.ConnectionString");
        //connectionString = "UseDevelopmentStorage=true;";
        configuration.UseSerialization<XmlSerializer>();
        configuration.UseTransport<AzureStorageQueueTransport>()
            .ConnectionString(connectionString)
            .MessageInvisibleTime(TimeSpan.FromSeconds(5))
            .SerializeMessageWrapperWith(definition => MessageWrapperSerializer.Xml.Value);

        endpointConfiguration = configuration;

        await CleanQueuesUsedByTest(connectionString, configuration);
    }

    public Task Cleanup()
    {
        return Task.FromResult(0);
    }

    private async Task CleanQueuesUsedByTest(string connectionString, BusConfiguration configuration)
    {
        var storage = CloudStorageAccount.Parse(connectionString);
        var queues = storage.CreateCloudQueueClient();

        var queuesNames = GetTestRelatedQueueNames(configuration);

        foreach (var queuesName in queuesNames)
        {
            var queue = queues.GetQueueReference(queuesName);
            if (await queue.ExistsAsync())
            {
                await queue.ClearAsync();
            }
        }
    }

    private IEnumerable<string> GetTestRelatedQueueNames(BusConfiguration configuration)
    {
        var bindings = endpointConfiguration.GetSettings().Get<QueueBindings>();
        var generator = new QueueAddressGenerator(endpointConfiguration.GetSettings());
        return bindings.ReceivingAddresses.Concat(bindings.SendingAddresses).Select(queue => generator.GetQueueName(queue));

    }
}
