using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using NServiceBus;
using NServiceBus.AcceptanceTesting.Support;
using NServiceBus.AcceptanceTests.ScenarioDescriptors;
using NUnit.Framework;

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
    public Task Configure(string endpointName, EndpointConfiguration configuration, RunSettings settings)
    {
        var connectionString = settings.Get<string>("Transport.ConnectionString");
        configuration
            .UseTransport<AzureStorageQueueTransport>()
            .ConnectionString(connectionString)
            .MessageInvisibleTime(TimeSpan.FromSeconds(5));
        //.SerializeMessageWrapperWith<JsonSerializer>();

        configuration.UseSerialization<XmlSerializer>();

        if (endpointName.StartsWith("RegisteringAdditionalDeserializers.CustomSerializationSender"))
        {
            Assert.Ignore("Ignored since this scenario is not supported by ASQ.");
        }

        var props = TestContext.CurrentContext.Test.Properties;

        if (props.ContainsKey("QueuesCleaned") == false)
        {
            props.Add("QueuesCleaned", true);

            return CleanQueuesUsedByTest(connectionString);
        }

        return Task.FromResult(0);
    }

    public Task Cleanup()
    {
        return Task.FromResult(0);
    }

    static Task CleanQueuesUsedByTest(string connectionString)
    {
        var storage = CloudStorageAccount.Parse(connectionString);
        var client = storage.CreateCloudQueueClient();
        var queues = GetTestRelatedQueues(client).ToArray();

        var tasks = new Task[queues.Length];
        for (var i = 0; i < queues.Length; i++)
        {
            tasks[i] = queues[i].ClearAsync();

        }

        return Task.WhenAll(tasks);
    }

    static IEnumerable<CloudQueue> GetTestRelatedQueues(CloudQueueClient queues)
    {
        // for now, return all
        return queues.ListQueues();
    }
}