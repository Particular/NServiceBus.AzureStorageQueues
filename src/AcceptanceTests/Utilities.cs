namespace Testing
{
    using System;
    using Azure.Core;
    using Azure.Core.Pipeline;
    using Azure.Data.Tables;
    using Azure.Storage.Blobs;
    using Azure.Storage.Queues;
    using NServiceBus;

    public static class Utilities
    {
        public static string GetEnvConfiguredConnectionString()
        {
            var environmentVariableName = $"{nameof(AzureStorageQueueTransport)}_ConnectionString";
            var connectionString = GetEnvironmentVariable(environmentVariableName);

            return string.IsNullOrEmpty(connectionString)
                ? "UseDevelopmentStorage=true"
                : connectionString;
        }

        public static string GetEnvConfiguredConnectionString2()
        {
            var environmentVariableName = $"{nameof(AzureStorageQueueTransport)}_ConnectionString_2";
            var connectionString = GetEnvironmentVariable(environmentVariableName);

            return string.IsNullOrEmpty(connectionString)
                ? "UseDevelopmentStorage=true"
                : connectionString;
        }

        static string GetEnvironmentVariable(string variable)
        {
            var candidate = Environment.GetEnvironmentVariable(variable, EnvironmentVariableTarget.User);
            return string.IsNullOrWhiteSpace(candidate) ? Environment.GetEnvironmentVariable(variable) : candidate;
        }

        public static AzureStorageQueueTransport CreateTransportWithDefaultTestsConfiguration(
            string connectionString,
            string delayedDeliveryPoisonQueue = null,
            HttpPipelinePolicy tableServiceClientPipelinePolicy = null)
        {
            ThrowIfPremiumEndpointConnectionString(connectionString);

            AzureStorageQueueTransport transport;

            var tableClientOptions = new TableClientOptions();
            if (tableServiceClientPipelinePolicy != null)
            {
                tableClientOptions.AddPolicy(tableServiceClientPipelinePolicy, HttpPipelinePosition.PerCall);
            }

            transport = new AzureStorageQueueTransport(
                new QueueServiceClient(connectionString),
                new BlobServiceClient(connectionString),
                new TableServiceClient(connectionString, tableClientOptions));

            return SetTransportDefaultTestsConfiguration(transport, delayedDeliveryPoisonQueue);
        }

        static void ThrowIfPremiumEndpointConnectionString(string connectionString)
        {
            Guard.AgainstNullAndEmpty(nameof(connectionString), connectionString);

            if (connectionString.Contains("https://localhost") ||
                connectionString.Contains(".table.cosmosdb.") ||
                connectionString.Contains(".table.cosmos."))
            {
                throw new Exception($"When configuring {nameof(AzureStorageQueueTransport)} with a single connection string, only Azure Storage connection can be used. See documentation for alternative options to configure the transport.");
            }
        }

        public static AzureStorageQueueTransport SetTransportDefaultTestsConfiguration(AzureStorageQueueTransport transport, string delayedDeliveryPoisonQueue = null)
        {
            transport.MessageInvisibleTime = TimeSpan.FromSeconds(30);
            transport.MessageWrapperSerializationDefinition = new TestIndependence.TestIdAppendingSerializationDefinition<SystemJsonSerializer>();
            transport.QueueNameSanitizer = BackwardsCompatibleQueueNameSanitizerForTests.Sanitize;

            if (delayedDeliveryPoisonQueue != null)
            {
                transport.DelayedDelivery.DelayedDeliveryPoisonQueue = delayedDeliveryPoisonQueue;
            }

            return transport;
        }
    }
}
