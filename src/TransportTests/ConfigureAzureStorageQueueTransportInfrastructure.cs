using System;
using System.Globalization;
using System.Reflection;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Logging;
using NServiceBus.Settings;
using NServiceBus.Transport;
using NServiceBus.Transport.AzureStorageQueues.TransportTests;
using NServiceBus.TransportTests;
using NServiceBus.Unicast.Messages;

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
            QueueNameSanitizer = BackwardsCompatibleQueueNameSanitizerForTests.Sanitize
        };

        return transport;
    }

    public async Task<TransportInfrastructure> Configure(TransportDefinition transportDefinition, HostSettings hostSettings, string inputQueueName, string errorQueueName)
    {
        if (hostSettings.CoreSettings.TryGet<MessageMetadataRegistry>(out var registry) == false)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.CreateInstance;

            var conventions = ((SettingsHolder)hostSettings.CoreSettings).GetOrCreate<Conventions>();
            registry = (MessageMetadataRegistry)Activator.CreateInstance(typeof(MessageMetadataRegistry), flags, null, new object[] { new Func<Type, bool>(t => conventions.IsMessageType(t)) }, CultureInfo.InvariantCulture);

            ((SettingsHolder)hostSettings.CoreSettings).Set(registry);
        }


        var transportInfrastructure = await transportDefinition.Initialize(
            hostSettings,
            new[]
            {
                new ReceiveSettings(inputQueueName, inputQueueName, true, false, errorQueueName),
            },
            new string[0]);

        return transportInfrastructure;
    }

    public Task Cleanup()
    {
        return Task.CompletedTask;
    }
}