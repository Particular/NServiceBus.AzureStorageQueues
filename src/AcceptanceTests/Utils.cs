namespace Testing
{
    using System;
    using NServiceBus;

    public static class Utillities
    {
        public static string GetEnvConfiguredConnectionString()
        {
            var environmentVartiableName = $"{nameof(AzureStorageQueueTransport)}_ConnectionString";
            var connectionString = GetEnvironmentVariable(environmentVartiableName);
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new Exception($"Oh no! We couldn't find an environment variable '{environmentVartiableName}' with Azure Storage connection string.");
            }

            return connectionString;

            string GetEnvironmentVariable(string variable)
            {
                var candidate = Environment.GetEnvironmentVariable(variable, EnvironmentVariableTarget.User);
                return string.IsNullOrWhiteSpace(candidate) ? Environment.GetEnvironmentVariable(variable) : candidate;
            }
        }

        public static string BuildAnotherConnectionString(string connectionString)
        {
            return connectionString + ";BlobEndpoint=https://notusedatall.blob.core.windows.net";
        }
    }

}