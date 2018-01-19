namespace NServiceBus.AcceptanceTests.WindowsAzureStorageQueues.Configuration
{
    using System;
    using System.Collections.Concurrent;
    using System.IO;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Azure.Transports.WindowsAzureStorageQueues;
    using Azure.Transports.WindowsAzureStorageQueues.AcceptanceTests;
    using EndpointTemplates;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Queue;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using NUnit.Framework;

    public class When_using_alias_instead_of_connection_string_for_default_account : NServiceBusAcceptanceTest
    {
        CloudQueue destinationQueue;

        public When_using_alias_instead_of_connection_string_for_default_account()
        {
            var connectionString = Utils.GetEnvConfiguredConnectionString();
            var account = CloudStorageAccount.Parse(connectionString);
            destinationQueue = account.CreateCloudQueueClient().GetQueueReference("destination");
        }

        [OneTimeSetUp]
        public Task OneTimeSetup()
        {
            return destinationQueue.CreateIfNotExistsAsync();
        }

        [Test]
        public async Task Should_send_messages_without_exposing_connection_string()
        {
            await Scenario.Define<Context>()
                .WithEndpoint<SenderEndpoint>(c => c.When(session => session.Send(destinationQueue.Name, new KickoffMessage())))
                .Done(c => true)
                .Run();

            var message = await destinationQueue.GetMessageAsync().ConfigureAwait(false);
            await destinationQueue.DeleteMessageAsync(message).ConfigureAwait(false);

            using (var reader = new JsonTextReader(new StreamReader(new MemoryStream(message.AsBytes))))
            {
                var token = JToken.ReadFrom(reader);
                var headers = token["Headers"];
                var replyTo = headers[Headers.ReplyToAddress];

                var senderEndpointName = AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(SenderEndpoint));
                var queueAddressGenerator = new QueueAddressGenerator();
                var replyToQueueName = queueAddressGenerator.GetQueueName(senderEndpointName);

                StringAssert.AreEqualIgnoringCase(replyToQueueName, ((JValue)token[nameof(MessageWrapper.ReplyToAddress)]).Value.ToString());
                StringAssert.AreEqualIgnoringCase(replyToQueueName, ((JValue)replyTo).Value.ToString());
            }
        }

        public class Context : ScenarioContext
        {
            public string ReplyToAddress { get; set; }
        }

        public class KickoffMessage : IMessage { }

        public class SenderEndpoint : EndpointConfigurationBuilder
        {
            public SenderEndpoint()
            {
                EndpointSetup<DefaultServer>(endpointConfiguration =>
                {
                    var transport = endpointConfiguration.UseTransport<AzureStorageQueueTransport>();
                    transport.UseAccountAliasesInsteadOfConnectionStrings();
                    transport.DefaultAccountAlias("defaultAlias");
                });
            }
        }

        /// <summary>
        /// Adopted from ASQ transport as it's opinionated about endpoint sanitization.
        /// </summary>
        class QueueAddressGenerator
        {
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

                var input = address.Replace('.', '-').ToLowerInvariant(); // this string was used in the past to calculate Guid, should stay backward compatible
                var shortenedName = Shortener.Md5(input);
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

            ConcurrentDictionary<string, string> sanitizedQueueNames = new ConcurrentDictionary<string, string>();

            static Regex invalidCharacters = new Regex(@"[^a-zA-Z0-9\-]", RegexOptions.Compiled);
            static Regex multipleDashes = new Regex(@"\-+", RegexOptions.Compiled);

            class Shortener
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
    }
}