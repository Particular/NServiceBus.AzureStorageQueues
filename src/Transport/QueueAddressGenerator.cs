namespace NServiceBus.Azure.Transports.WindowsAzureStorageQueues
{
    using System;
    using System.Text.RegularExpressions;
    using NServiceBus.AzureStorageQueues;
    using NServiceBus.Settings;

    /// <summary>
    ///     Helper class
    /// </summary>
    public class QueueAddressGenerator
    {
        readonly Func<string, string> shortener;

        public QueueAddressGenerator(ReadOnlySettings settings)
        {
            if (settings.GetOrDefault<bool>(WellKnownConfigurationKeys.Sha1Shortener))
            {
                shortener = Shortener.Sha1;
            }
            else
            {
                shortener = Shortener.Md5;
            }
        }

        public string GetQueueName(string address)
        {
            var name = SanitizeQueueName(address.ToLowerInvariant());
            var input = address.Replace('.', '-').ToLowerInvariant(); // this string was used in the past to calculate guid, should stay backward compat

            if (name.Length > 63)
            {
                var shortenedName = shortener(input);
                name = name.Substring(0, 63 - shortenedName.Length - 1).Trim('-') + "-" + shortenedName;
            }

            return name;
        }

        static string SanitizeQueueName(string queueName)
        {
            //rules for naming queues can be found at http://msdn.microsoft.com/en-us/library/windowsazure/dd179349.aspx"
            var invalidCharacters = new Regex(@"[^a-zA-Z0-9\-]");
            var sanitized = invalidCharacters.Replace(queueName, "-"); // this can lead to multiple - occurences in a row
            var multipleDashes = new Regex(@"\-+");
            sanitized = multipleDashes.Replace(sanitized, "-");
            return sanitized;
        }
    }
}