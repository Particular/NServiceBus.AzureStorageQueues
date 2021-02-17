#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable CS0618 // Type or member is obsolete

namespace NServiceBus.Transport.AzureStorageQueues.AcceptanceTests
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Azure.Transports.WindowsAzureStorageQueues;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using global::Azure.Storage.Queues;
    using global::Azure.Storage.Queues.Models;
    using global::Newtonsoft.Json;
    using global::Newtonsoft.Json.Linq;
    using NUnit.Framework;
    using Testing;

    public class When_using_alias_instead_of_connection_string_for_default_account : NServiceBusAcceptanceTest
    {
        QueueClient destinationQueue;

        public When_using_alias_instead_of_connection_string_for_default_account()
        {
            var connectionString = Utilities.GetEnvConfiguredConnectionString();
            destinationQueue = new QueueClient(connectionString, "destination");
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

            QueueMessage[] messages = await destinationQueue.ReceiveMessagesAsync(1).ConfigureAwait(false);
            var message = messages[0];
            await destinationQueue.DeleteMessageAsync(message.MessageId, message.PopReceipt).ConfigureAwait(false);

            var bytes = Convert.FromBase64String(message.MessageText);
            using (var reader = new JsonTextReader(new StreamReader(new MemoryStream(bytes))))
            {
                var token = JToken.ReadFrom(reader);
                var headers = token["Headers"];
                var replyTo = headers[Headers.ReplyToAddress];

                var senderEndpointName = AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(SenderEndpoint));
                var replyToQueueName = BackwardsCompatibleQueueNameSanitizerForTests.Sanitize(senderEndpointName);

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
                    endpointConfiguration.UseSerialization<NewtonsoftSerializer>();
                    var transport = endpointConfiguration.ConfigureTransport<AzureStorageQueueTransport>();
                    transport.AccountRouting.DefaultAccountAlias = "defaultAlias";
                });
            }
        }
    }
}

#pragma warning restore CS0618 // Type or member is obsolete
#pragma warning restore IDE0079 // Remove unnecessary suppression