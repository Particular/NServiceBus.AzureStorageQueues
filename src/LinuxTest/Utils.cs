using System;

public static class Utils
{
    public static string GetEnvConfiguredConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable("AzureStorageQueueTransport.ConnectionString")
            .Replace("\\;", ";").Replace("'", "");
        return connectionString;
    }
}
