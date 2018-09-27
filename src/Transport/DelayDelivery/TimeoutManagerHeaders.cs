namespace NServiceBus.Transport.AzureStorageQueues
{
    static class TimeoutManagerHeaders
    {
        public const string Expire = "NServiceBus.Timeout.Expire";
        public const string RouteExpiredTimeoutTo = "NServiceBus.Timeout.RouteExpiredTimeoutTo";
    }
}