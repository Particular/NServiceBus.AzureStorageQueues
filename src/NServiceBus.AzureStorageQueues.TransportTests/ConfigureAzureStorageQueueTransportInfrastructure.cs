using System;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Azure.Transports.WindowsAzureStorageQueues.TransportTests;
using NServiceBus.Logging;
using NServiceBus.Settings;
using NServiceBus.TransportTests;

public class ConfigureAzureStorageQueueTransportInfrastructure : IConfigureTransportInfrastructure
{
    public TransportConfigurationResult Configure(SettingsHolder settings, TransportTransactionMode transactionMode)
    {
        LogManager.UseFactory(new ConsoleLoggerFactory());

        return new TransportConfigurationResult
        {
            TransportInfrastructure = new AzureStorageQueueTransport().Initialize(settings, Environment.GetEnvironmentVariable("AzureStorageQueueTransport.ConnectionString")),
            PurgeInputQueueOnStartup = false
        };
    }

    public Task Cleanup()
    {
        return Task.FromResult(0);
    }
}