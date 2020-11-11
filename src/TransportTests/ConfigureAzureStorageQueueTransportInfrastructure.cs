using System;
using System.Globalization;
using System.Reflection;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Transport.AzureStorageQueues.TransportTests;
using NServiceBus.Logging;
using NServiceBus.Serialization;
using NServiceBus.Settings;
using NServiceBus.TransportTests;
using NServiceBus.Unicast.Messages;

public class ConfigureAzureStorageQueueTransportInfrastructure : IConfigureTransportInfrastructure
{
    public TransportConfigurationResult Configure(SettingsHolder settings, TransportTransactionMode transactionMode)
    {
        LogManager.UseFactory(new ConsoleLoggerFactory());

        if (settings.TryGet<MessageMetadataRegistry>(out var registry) == false)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.CreateInstance;

            var conventions = settings.GetOrCreate<Conventions>();
            registry = (MessageMetadataRegistry)Activator.CreateInstance(typeof(MessageMetadataRegistry), flags, null, new object[] { new Func<Type, bool>(t => conventions.IsMessageType(t)) }, CultureInfo.InvariantCulture);

            settings.Set(registry);
        }

        settings.Set(AzureStorageQueueTransport.SerializerSettingsKey, Tuple.Create<SerializationDefinition, SettingsHolder>(new XmlSerializer(), settings));

        var transportExtension = new TransportExtensions<AzureStorageQueueTransport>(settings);
        transportExtension.SanitizeQueueNamesForTesting(BackwardsCompatibleQueueNameSanitizerForTests.Sanitize);

        return new TransportConfigurationResult
        {
            TransportInfrastructure = new AzureStorageQueueTransport().Initialize(settings, Testing.Utilities.GetEnvConfiguredConnectionString()),
            PurgeInputQueueOnStartup = false
        };
    }

    public Task Cleanup()
    {
        return Task.FromResult(0);
    }
}