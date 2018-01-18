namespace NServiceBus.AcceptanceTests.WindowsAzureStorageQueues.Configuration
{
    using System.IO;
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
        static string EndpointName = AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(SenderEndpoint));

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

                StringAssert.AreEqualIgnoringCase(EndpointName, ((JValue)token[nameof(MessageWrapper.ReplyToAddress)]).Value.ToString());
                StringAssert.AreEqualIgnoringCase(EndpointName, ((JValue)replyTo).Value.ToString());
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

                    endpointConfiguration.OverrideLocalAddress(EndpointName);
                });
            }
        }
    }
}