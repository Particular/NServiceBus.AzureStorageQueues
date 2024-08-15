namespace NServiceBus.Transport.AzureStorageQueues.AcceptanceTests
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using global::Azure.Storage.Queues;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;
    using Testing;

    public class When_using_alias_instead_of_connection_string_for_default_account : NServiceBusAcceptanceTest
    {
        readonly QueueClient destinationQueue;

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
            var ctx = await Scenario.Define<Context>()
                .WithEndpoint<SenderEndpoint>(c => c.When(session => session.Send(destinationQueue.Name, new KickoffMessage())))
                .Done(c => true)
                .Run();

            var envelope = await RawMessageReceiver.Receive(destinationQueue, ctx.TestRunId.ToString());

            var senderEndpointName = AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(SenderEndpoint));

            Assert.That(envelope.Headers[Headers.OriginatingEndpoint], Is.EqualTo(senderEndpointName));

            var replyToAddress = BackwardsCompatibleQueueNameSanitizerForTests.Sanitize(senderEndpointName);

            Assert.Multiple(() =>
            {
                Assert.That(envelope.ReplyToAddress, Is.EqualTo(replyToAddress));
                Assert.That(envelope.Headers[Headers.ReplyToAddress], Is.EqualTo(replyToAddress));
            });
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
                    var transport = endpointConfiguration.ConfigureTransport<AzureStorageQueueTransport>();
                    transport.AccountRouting.DefaultAccountAlias = "defaultAlias";
                });
        }
    }
}