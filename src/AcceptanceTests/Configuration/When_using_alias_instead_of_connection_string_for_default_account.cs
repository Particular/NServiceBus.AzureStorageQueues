namespace NServiceBus.Transport.AzureStorageQueues.AcceptanceTests
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Azure.Transports.WindowsAzureStorageQueues;
    using global::Azure.Storage.Queues;
    using global::Azure.Storage.Queues.Models;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
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
        public Task OneTimeSetup() => destinationQueue.CreateIfNotExistsAsync();

        [Test]
        public async Task Should_send_messages_without_exposing_connection_string()
        {
            await Scenario.Define<Context>()
                .WithEndpoint<SenderEndpoint>(c => c.When(session => session.Send(destinationQueue.Name, new KickoffMessage())))
                .Done(c => true)
                .Run();

            QueueMessage[] messages = await destinationQueue.ReceiveMessagesAsync(1);
            var message = messages[0];
            await destinationQueue.DeleteMessageAsync(message.MessageId, message.PopReceipt);

            var bytes = Convert.FromBase64String(message.MessageText);
            using var reader = new JsonTextReader(new StreamReader(new MemoryStream(bytes)));
            var token = await JToken.ReadFromAsync(reader);
            var headers = token["Headers"];
            var replyTo = headers[Headers.ReplyToAddress];

            var senderEndpointName = AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(SenderEndpoint));
            var replyToQueueName = BackwardsCompatibleQueueNameSanitizerForTests.Sanitize(senderEndpointName);

            StringAssert.AreEqualIgnoringCase(replyToQueueName, ((JValue)token[nameof(MessageWrapper.ReplyToAddress)]).Value.ToString());
            StringAssert.AreEqualIgnoringCase(replyToQueueName, ((JValue)replyTo).Value.ToString());
        }

        public class Context : ScenarioContext
        {
            public string ReplyToAddress { get; set; }
        }

        public class KickoffMessage : IMessage { }

        public class SenderEndpoint : EndpointConfigurationBuilder
        {
            public SenderEndpoint() =>
                EndpointSetup<DefaultServer>(endpointConfiguration =>
                {
                    endpointConfiguration.UseSerialization<SystemJsonSerializer>();
                    var transport = endpointConfiguration.ConfigureTransport<AzureStorageQueueTransport>();
                    transport.AccountRouting.DefaultAccountAlias = "defaultAlias";
                });
        }
    }
}