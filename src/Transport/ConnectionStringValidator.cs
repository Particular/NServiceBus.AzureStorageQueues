namespace NServiceBus.Transport.AzureStorageQueues
{
    using System;

    static class ConnectionStringValidator
    {
        public static void ThrowIfPremiumEndpointConnectionString(string connectionString)
        {
            if (IsPremiumEndpoint(connectionString))
            {
                throw new Exception($"When configuring {nameof(AzureStorageQueueTransport)} with a single connection string, only Azure Storage connection can be used. See documentation for alternative options to configure the transport.");
            }
        }

        // Adopted from Cosmos DB Table API SDK that uses similar approach to change the underlying execution
        static bool IsPremiumEndpoint(string connectionString)
        {
            var lowerInvariant = connectionString.ToLowerInvariant();
            return lowerInvariant.Contains("https://localhost") || lowerInvariant.Contains(".table.cosmosdb.") || lowerInvariant.Contains(".table.cosmos.");
        }
    }
}