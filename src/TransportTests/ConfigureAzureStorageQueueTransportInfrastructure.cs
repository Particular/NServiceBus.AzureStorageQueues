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
            connectionString = "UseDevelopmentStorage=true";
        }

        var transport = new AzureStorageQueueTransport(connectionString)
        {
            MessageWrapperSerializationDefinition = new XmlSerializer(),
            QueueNameSanitizer = BackwardsCompatibleQueueNameSanitizerForTests.Sanitize,
            Subscriptions = { DisableCaching = true }
        };
        transport.DelayedDelivery.DelayedDeliveryPoisonQueue = "error";

        return transport;
    }

    public async Task<TransportInfrastructure> Configure(TransportDefinition transportDefinition, HostSettings hostSettings, QueueAddress inputQueueName, string errorQueueName, CancellationToken cancellationToken = default)
    {
        var transportInfrastructure = await transportDefinition.Initialize(
            hostSettings,
            new[]
            {
                new ReceiveSettings(inputQueueName.ToString(), inputQueueName, true, false, errorQueueName),
            },
            Array.Empty<string>(),
            cancellationToken);

        return transportInfrastructure;
    }

    public Task Cleanup(CancellationToken cancellationToken = default) => Task.CompletedTask;
}