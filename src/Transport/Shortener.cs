namespace NServiceBus.Azure.Transports.WindowsAzureStorageQueues
{
    using System;
    using System.Security.Cryptography;
    using System.Text;

    public class Shortener
    {
        public static string Md5(string test)
        {
            //use MD5 hash to get a 16-byte hash of the string
            using (var provider = new MD5CryptoServiceProvider())
            {
                var inputBytes = Encoding.Default.GetBytes(test);
                var hashBytes = provider.ComputeHash(inputBytes);
                //generate a guid from the hash:
                return new Guid(hashBytes).ToString();
            }
        }

        public static string Sha1(string test)
        {
            using (var provider = new SHA1CryptoServiceProvider())
            {
                var inputBytes = Encoding.Default.GetBytes(test);
                var hashBytes = provider.ComputeHash(inputBytes);

                return ToChars(hashBytes);
            }
        }

        // ReSharper disable once SuggestBaseTypeForParameter
        static string ToChars(byte[] hashBytes)
        {
            var chars = new char[hashBytes.Length*2];
            for (var i = 0; i < chars.Length; i += 2)
            {
                var byteIndex = i/2;
                chars[i] = HexToChar((byte) (hashBytes[byteIndex] >> 4));
                chars[i + 1] = HexToChar(hashBytes[byteIndex]);
            }

            return new string(chars);
        }

        static char HexToChar(byte a)
        {
            a &= 15;
            return a > 9 ? (char) (a - 10 + 97) : (char) (a + 48);
        }
    }
}