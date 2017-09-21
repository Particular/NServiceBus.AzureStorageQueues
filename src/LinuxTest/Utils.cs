using System;

public static class Utils
{
    public static string GetEnvConfiguredConnectionString()
    {
        return Environment.GetEnvironmentVariable("AzureStorageQueueTransportConnectionString");
    }
}
