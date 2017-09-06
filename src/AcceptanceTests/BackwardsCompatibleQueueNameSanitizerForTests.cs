using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

static class BackwardsCompatibleQueueNameSanitizerForTests
{
    public static string Sanitize(string queueName)
    {
        var queueNameInLowerCase = queueName.ToLowerInvariant();
        return ShortenQueueNameIfNecessary(queueNameInLowerCase, SanitizeQueueName(queueNameInLowerCase));
    }

    static string ShortenQueueNameIfNecessary(string address, string queueName)
    {
        if (queueName.Length <= 63)
        {
            return queueName;
        }

        var shortenedName = Shortener.Md5(address);
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

    static Regex invalidCharacters = new Regex(@"[^a-zA-Z0-9\-]", RegexOptions.Compiled);
    static Regex multipleDashes = new Regex(@"\-+", RegexOptions.Compiled);

    class Shortener
    {
        public static string Md5(string test)
        {
            //use MD5 hash to get a 16-byte hash of the string
            using (var provider = MD5.Create())
            {
                var inputBytes = Encoding.Default.GetBytes(test);
                var hashBytes = provider.ComputeHash(inputBytes);
                //generate a guid from the hash:
                return new Guid(hashBytes).ToString();
            }
        }

        static string ToChars(byte[] hashBytes)
        {
            var chars = new char[hashBytes.Length * 2];
            for (var i = 0; i < chars.Length; i += 2)
            {
                var byteIndex = i / 2;
                chars[i] = HexToChar((byte)(hashBytes[byteIndex] >> 4));
                chars[i + 1] = HexToChar(hashBytes[byteIndex]);
            }

            return new string(chars);
        }

        static char HexToChar(byte a)
        {
            a &= 15;
            return a > 9 ? (char)(a - 10 + 97) : (char)(a + 48);
        }
    }
}