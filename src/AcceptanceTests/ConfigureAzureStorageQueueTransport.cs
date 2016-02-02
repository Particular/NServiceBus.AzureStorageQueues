using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting.Support;

class ConfigureAzureStorageQueueTransport : IConfigureTestExecution
{
    public Task Configure(BusConfiguration configuration, IDictionary<string, string> settings)
    {
        var connectionString = settings["Transport.ConnectionString"];
        NamespaceSetUp.ConnectionString = connectionString;
        configuration.UseTransport<AzureStorageQueueTransport>()
            .ConnectionString(connectionString)
            .MessageInvisibleTime(TimeSpan.FromSeconds(5));

        return Task.FromResult(0);
    }

    public async Task Cleanup()
    {
        await Task.FromResult(0);
    }
}