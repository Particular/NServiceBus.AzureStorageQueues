using System.Collections.Generic;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting.Support;

class ConfigureAzureStorageQueueTransport : IConfigureTestExecution
{
    public Task Configure(BusConfiguration configuration, IDictionary<string, string> settings)
    {
        var connectionString = settings["Transport.ConnectionString"];
        NamespaceSetUp.SetConnection(connectionString);
        configuration.UseTransport<AzureStorageQueueTransport>().ConnectionString(connectionString);
        return Task.FromResult(0);
    }

    public async Task Cleanup()
    {
        await Task.FromResult(0);
    }
}