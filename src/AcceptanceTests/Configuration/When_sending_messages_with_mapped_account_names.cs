namespace NServiceBus.Transport.AzureStorageQueues.AcceptanceTests
{
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using global::Azure.Data.Tables;
    using global::Azure.Storage.Queues;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NServiceBus.Azure.Transports.WindowsAzureStorageQueues;
    using NUnit.Framework;
    using Testing;

    public class When_sending_messages_with_mapped_account_names : NServiceBusAcceptanceTest
    {
        [OneTimeSetUp]
        public async Task Setup()
        {
            // Set up receiver queue on 2nd storage account
            var queueServiceClient = new QueueServiceClient(Utilities.GetEnvConfiguredConnectionString2());
            _ = await queueServiceClient.CreateQueueAsync(ReceiverName);
            _ = await queueServiceClient.CreateQueueAsync(AuditQueueName);
            _ = await queueServiceClient.CreateQueueAsync("error");
        }

        [Test]
        public async Task Is_enabled_and_single_account_is_used_Should_audit_just_queue_name_without_account()
        {
            var connectionString = Utilities.GetEnvConfiguredConnectionString();

            var envelope = await SendMessage<ReceiverUsingOneMappedConnectionString>(connectionString, ReceiverName);

            Assert.Multiple(() =>
            {
                Assert.That(envelope.Headers.Values.Any(v => v.Contains(connectionString)), Is.False, "Message headers should not include the raw connection string");
                Assert.That(envelope.Headers[Headers.OriginatingEndpoint], Is.EqualTo(SenderName));
                Assert.That(envelope.ReplyToAddress, Is.EqualTo(SenderName));
                Assert.That(envelope.Headers[Headers.ReplyToAddress], Is.EqualTo(SenderName));
            });
        }

        [Test]
        public async Task Is_enabled_and_sending_to_another_account_Should_audit_fully_qualified_queue()
        {
            var connectionString = Utilities.GetEnvConfiguredConnectionString2();

            var envelope = await SendMessage<ReceiverUsingMappedConnectionStrings>(connectionString, $"{ReceiverName}@{AnotherConnectionStringName}");

            Assert.Multiple(() =>
            {
                Assert.That(envelope.Headers.Values.Any(v => v.Contains(connectionString)), Is.False, "Message headers should not include the raw connection string");
                Assert.That(envelope.Headers[Headers.OriginatingEndpoint], Is.EqualTo(SenderName));
            });

            var replyToAddress = $"{SenderName}@{DefaultConnectionStringName}";

            Assert.Multiple(() =>
            {
                Assert.That(envelope.Headers[Headers.ReplyToAddress], Is.EqualTo(replyToAddress));
                Assert.That(envelope.ReplyToAddress, Is.EqualTo(replyToAddress));
            });
        }

        static async Task<MessageWrapper> SendMessage<TReceiver>(string destinationConnectionString, string destination, CancellationToken cancellationToken = default)
            where TReceiver : EndpointConfigurationBuilder, new()
        {
            var ctx = await Scenario.Define<Context>()
                .WithEndpoint<Sender>(b => b.When(s =>
                {
                    var options = new SendOptions();
                    options.SetDestination(destination);
                    return s.Send(new MyCommand(), options, cancellationToken);
                }))
                .WithEndpoint<TReceiver>()
                .Run(cancellationToken);

            Assert.That(ctx.Received, Is.True);

            return await RawMessageReceiver.Receive(destinationConnectionString, AuditQueueName, ctx.TestRunId.ToString(), cancellationToken);
        }

        const string SenderName = "mapping-names-sender";
        const string ReceiverName = "mapping-names-receiver";
        const string AuditQueueName = "mapping-names-audit";
        const string DefaultConnectionStringName = "default_account";
        const string AnotherConnectionStringName = "another_account";

        class Context : ScenarioContext
        {
            public bool Received { get; set; }
        }

        class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServer>(cfg =>
                {
                    var transport = cfg.ConfigureTransport<AzureStorageQueueTransport>();
                    transport.AccountRouting.DefaultAccountAlias = DefaultConnectionStringName;
                    transport.AccountRouting.AddAccount(
                        AnotherConnectionStringName,
                        new QueueServiceClient(Utilities.GetEnvConfiguredConnectionString2()),
                        new TableServiceClient(Utilities.GetEnvConfiguredConnectionString2()));
                });
                CustomEndpointName(SenderName);
            }
        }

        class ReceiverUsingMappedConnectionStrings : EndpointConfigurationBuilder
        {
            public ReceiverUsingMappedConnectionStrings()
            {
                var transport = new AzureStorageQueueTransport(Utilities.GetEnvConfiguredConnectionString2(), useNativeDelayedDeliveries: false);
                EndpointSetup(new CustomizedServer(transport), (cfg, runDescriptor) =>
                {
                    cfg.AuditProcessedMessagesTo(AuditQueueName);

                    transport.AccountRouting.DefaultAccountAlias = AnotherConnectionStringName;
                    transport.AccountRouting.AddAccount(
                        DefaultConnectionStringName,
                        new QueueServiceClient(Utilities.GetEnvConfiguredConnectionString()),
                        new TableServiceClient(Utilities.GetEnvConfiguredConnectionString()));
                });

                CustomEndpointName(ReceiverName);
            }
        }
        class ReceiverUsingOneMappedConnectionString : EndpointConfigurationBuilder
        {
            public ReceiverUsingOneMappedConnectionString()
            {
                EndpointSetup<DefaultPublisher>(cfg =>
                {
                    cfg.AuditProcessedMessagesTo(AuditQueueName);

                    var transport = cfg.ConfigureTransport<AzureStorageQueueTransport>();
                    transport.AccountRouting.DefaultAccountAlias = DefaultConnectionStringName;
                    transport.AccountRouting.AddAccount(
                        AnotherConnectionStringName,
                        new QueueServiceClient(Utilities.GetEnvConfiguredConnectionString2()),
                        new TableServiceClient(Utilities.GetEnvConfiguredConnectionString2()));
                });
                CustomEndpointName(ReceiverName);
            }
        }

        class Handler : IHandleMessages<MyCommand>
        {
            public Handler(Context testContext) => this.testContext = testContext;

            public Task Handle(MyCommand message, IMessageHandlerContext context)
            {
                testContext.Received = true;
                testContext.MarkAsCompleted();
                return Task.CompletedTask;
            }

            readonly Context testContext;
        }

        public class MyCommand : ICommand
        {
        }
    }
}