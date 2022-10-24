namespace NServiceBus.Transport.AzureStorageQueues.AcceptanceTests
{
    using System;

    static class StringExtensions
    {
        public static bool Contains(this string source, string subString, StringComparison comparison) => source.IndexOf(subString, comparison) >= 0;
    }
}
