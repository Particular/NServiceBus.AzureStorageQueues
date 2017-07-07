namespace NServiceBus.AzureStorageQueues
{
    using System;
    using System.Collections.Concurrent;
    using System.Text.RegularExpressions;
    using Config;
    using Settings;

    class QueueAddressGenerator
    {
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
            var queueName = address.ToLowerInvariant();

            return sanitizedQueueNames.GetOrAdd(queueName, name => ShortenQueueNameIfNecessary(name, SanitizeQueueName(name)));
        }

        string ShortenQueueNameIfNecessary(string address, string queueName)
        {
            if (queueName.Length <= 63)
            {
                return queueName;
            }

            var input = address.Replace('.', '-').ToLowerInvariant(); // this string was used in the past to calculate Guid, should stay backward compatibility
            var shortenedName = shortener(input);
            queueName = $"{queueName.Substring(0, 63 - shortenedName.Length - 1).Trim('-')}-{shortenedName}";
            return queueName;
        }

        static string SanitizeQueueName(string queueName)
        {
            //rules for naming queues can be found at http://msdn.microsoft.com/en-us/library/windowsazure/dd179349.aspx"
            var sanitized = invalidCharacters.Replace(queueName, "-"); // this can lead to multiple - occurrences in a row
            sanitized = multipleDashes.Replace(sanitized, "-");
            return sanitized;
        }

        Func<string, string> shortener;
        ConcurrentDictionary<string, string> sanitizedQueueNames = new ConcurrentDictionary<string, string>();

        static Regex invalidCharacters = new Regex(@"[^a-zA-Z0-9\-]", RegexOptions.Compiled);
        static Regex multipleDashes = new Regex(@"\-+", RegexOptions.Compiled);
    }
}