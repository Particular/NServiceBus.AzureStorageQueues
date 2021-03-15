using System;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Logging;
using NServiceBus.Transport;
using NServiceBus.Transport.AzureStorageQueues.TransportTests;
using NServiceBus.TransportTests;

public class ConfigureAzureStorageQueueTransportInfrastructure : IConfigureTransportInfrastructure
{
    public TransportDefinition CreateTransportDefinition()
    {
        LogManager.UseFactory(new ConsoleLoggerFactory());

        var connectionStringEnvVarName = "AzureStorageQueueTransport_ConnectionString";
        var connectionString = Environment.GetEnvironmentVariable(connectionStringEnvVarName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new Exception(
                $"Connection string is required for Acceptance tests. Set it in an environment variable named '{connectionStringEnvVarName}'");
        }

        var transport = new AzureStorageQueueTransport(connectionString)
        {
            MessageWrapperSerializationDefinition = new XmlSerializer(),
            QueueNameSanitizer = BackwardsCompatibleQueueNameSanitizerForTests.Sanitize,
            Subscriptions = { DisableCaching = true }
        };

        return transport;
    }

    public async Task<TransportInfrastructure> Configure(TransportDefinition transportDefinition, HostSettings hostSettings, string inputQueueName, string errorQueueName, CancellationToken cancellationToken = default)
    {
        var transportInfrastructure = await transportDefinition.Initialize(
            hostSettings,
            new[]
            {
                new ReceiveSettings(inputQueueName, inputQueueName, true, false, errorQueueName),
            },
            new string[0],
            cancellationToken);

        return transportInfrastructure;
    }

    public Task Cleanup(CancellationToken cancellationToken = default) => Task.CompletedTask;
}