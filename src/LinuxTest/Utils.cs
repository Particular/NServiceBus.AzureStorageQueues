using System;

public static class Utils
{
    public static string GetEnvConfiguredConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable("AzureStorageQueueTransportConnectionString")
            .Replace("\\;", ";").Replace("'", "");
        return connectionString;
    }
}
