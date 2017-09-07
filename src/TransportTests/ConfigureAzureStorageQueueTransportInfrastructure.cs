using System;
using System.Globalization;
using System.Reflection;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Azure.Transports.WindowsAzureStorageQueues.TransportTests;
using NServiceBus.Logging;
using NServiceBus.Settings;
using NServiceBus.TransportTests;
using NServiceBus.Unicast.Messages;
using NUnit.Framework;

public class ConfigureAzureStorageQueueTransportInfrastructure : IConfigureTransportInfrastructure
{
    public TransportConfigurationResult Configure(SettingsHolder settings, TransportTransactionMode transactionMode)
    {
        LogManager.UseFactory(new ConsoleLoggerFactory());

        MessageMetadataRegistry registry;
        if (settings.TryGet(out registry) == false)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public |BindingFlags.NonPublic | BindingFlags.CreateInstance;
            
            registry = (MessageMetadataRegistry) Activator.CreateInstance(typeof(MessageMetadataRegistry), flags, null, new object[] {settings.GetOrCreate<Conventions>()}, CultureInfo.InvariantCulture);

            settings.Set<MessageMetadataRegistry>(registry);
        }

        var methodName = TestContext.CurrentContext.Test.MethodName;
        if (methodName == nameof(When_on_error_throws.Should_reinvoke_on_error_with_original_exception))
        {
            throw new IgnoreException("ASQ uses a circuit breaker that is triggered after specific period of time. Critical errors are not reported immediately");
        }

        var transportExtension = new TransportExtensions<AzureStorageQueueTransport>(settings);
        transportExtension.SanitizeQueueNamesWith(BackwardsCompatibleQueueNameSanitizerForTests.Sanitize);

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