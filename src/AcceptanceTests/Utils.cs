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
        }

        public static string GetEnvConfiguredConnectionString2()
        {
            var environmentVartiableName = $"{nameof(AzureStorageQueueTransport)}_ConnectionString_2";
            var connectionString = GetEnvironmentVariable(environmentVartiableName);
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new Exception($"Oh no! We couldn't find an environment variable '{environmentVartiableName}' with Azure Storage connection string.");
            }

            return connectionString;
        }

        static string GetEnvironmentVariable(string variable)
        {
            var candidate = Environment.GetEnvironmentVariable(variable, EnvironmentVariableTarget.User);
            return string.IsNullOrWhiteSpace(candidate) ? Environment.GetEnvironmentVariable(variable) : candidate;
        }
    }
}