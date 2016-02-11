using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting.Support;
using NServiceBus.Azure.Transports.WindowsAzureStorageQueues;
using NServiceBus.AzureStorageQueues.AcceptanceTests;

class ConfigureAzureStorageQueueTransport : IConfigureTestExecution
{
    public Task Configure(BusConfiguration configuration, IDictionary<string, string> settings)
    {
        var connectionString = settings["Transport.ConnectionString"];
        configuration.UseSerialization<JsonSerializer>();
        configuration.UseTransport<AzureStorageQueueTransport>()
            .ConnectionString(connectionString)
            .MessageInvisibleTime(TimeSpan.FromSeconds(5))
            .SerializeMessageWrapperWith(defintion => MessageWrapperSerializer.Json.Value)
            .CreateSendingQueues();

        configuration.RegisterComponents(c => { c.ConfigureComponent<TestIndependenceMutator>(DependencyLifecycle.SingleInstance); });
        configuration.Pipeline.Register("TestIndependenceBehavior", typeof(TestIndependenceSkipBehavior),
            "Skips messages not created during the current test.");

        return Task.FromResult(0);
    }

    public Task Cleanup()
    {
        // nothing to clean as TestIndependenceSkipBehavior is registered
        return Task.FromResult(0);
    }
}