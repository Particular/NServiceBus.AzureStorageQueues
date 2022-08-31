namespace NServiceBus.Transport.AzureStorageQueues.AcceptanceTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using global::Azure.Storage.Queues;
    using global::Azure.Storage.Queues.Models;
    using global::Newtonsoft.Json;
    using global::Newtonsoft.Json.Linq;
    using Microsoft.Azure.Cosmos.Table;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
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
            _ = await queueServiceClient.CreateQueueAsync(AuditName);
            _ = await queueServiceClient.CreateQueueAsync("error");
        }

        [Test]
        public async Task Is_enabled_and_single_account_is_used_Should_audit_just_queue_name_without_account()
        {
            var ctx = await SendMessage<ReceiverUsingOneMappedConnectionString>(ReceiverName, Utilities.GetEnvConfiguredConnectionString()).ConfigureAwait(false);
            CollectionAssert.IsEmpty(ctx.ContainingRawConnectionString, "Message headers should not include raw connection string");

            foreach (var propertyWithSenderName in ctx.AllPropertiesFlattened.Where(property => property.Value.Contains(SenderName)))
            {
                Assert.AreEqual(SenderName, propertyWithSenderName.Value);
            }
        }

        [Test]
        public async Task Is_enabled_and_sending_to_another_account_Should_audit_fully_qualified_queue()
        {
            var ctx = await SendMessage<ReceiverUsingMappedConnectionStrings>($"{ReceiverName}@{AnotherConnectionStringName}", Utilities.GetEnvConfiguredConnectionString2()).ConfigureAwait(false);
            CollectionAssert.IsEmpty(ctx.ContainingRawConnectionString, "Message headers should not include raw connection string");

            var excluded = new HashSet<string>
            {
                Headers.OriginatingEndpoint
            };

            foreach (var propertyWithSenderName in ctx.AllPropertiesFlattened.Where(property => property.Value.Contains(SenderName)))
            {
                if (excluded.Contains(propertyWithSenderName.Key))
                {
                    continue;
                }

                var expected = $"{SenderName}@{DefaultConnectionStringName}";
                Assert.AreEqual(expected, propertyWithSenderName.Value, propertyWithSenderName.Key);
            }
        }

        async Task<Context> SendMessage<TReceiver>(string destination, string destinationConnectionString, CancellationToken cancellationToken = default)
            where TReceiver : EndpointConfigurationBuilder
        {
            var ctx = await Scenario.Define<Context>()
                .WithEndpoint<Sender>(b => b.When(s =>
                {
                    var options = new SendOptions();
                    options.SetDestination(destination);
                    return s.Send(new MyCommand(), options);
                }))
                .WithEndpoint<TReceiver>()
                .Done(c => c.Received)
                .Run().ConfigureAwait(false);

            Assert.IsTrue(ctx.Received);

            Dictionary<string, string> propertiesFlattened;
            do
            {
                cancellationToken.ThrowIfCancellationRequested();

                var receiverAuditQueue = new QueueClient(destinationConnectionString, AuditName);

                QueueMessage[] rawMessages = await receiverAuditQueue.ReceiveMessagesAsync(1, cancellationToken: cancellationToken).ConfigureAwait(false);
                if (rawMessages.Length == 0)
                {
                    Assert.Fail("No message in the audit queue to pick up.");
                }
                var rawMessage = rawMessages[0];
                await receiverAuditQueue.DeleteMessageAsync(rawMessage.MessageId, rawMessage.PopReceipt, cancellationToken).ConfigureAwait(false);

                JToken message;
                var bytes = Convert.FromBase64String(rawMessage.MessageText);
                using (var reader = new JsonTextReader(new StreamReader(new MemoryStream(bytes))))
                {
                    message = JToken.ReadFrom(reader);
                }

                propertiesFlattened = message.FindProperties(IsSimpleProperty)
                    .ToDictionary(jp => jp.Name, jp => ((JValue)jp.Value).Value<string>());

                if (propertiesFlattened.ContainsValue(ctx.TestRunId.ToString()))
                {
                    break;
                }
            }
            while (true);


            ctx.AllPropertiesFlattened = propertiesFlattened;

            ctx.ContainingRawConnectionString = ctx.AllPropertiesFlattened.Where(kvp => kvp.Value.Contains(Utilities.GetEnvConfiguredConnectionString()))
                .Select(kvp => kvp.Key).ToArray();

            return ctx;
        }

        static bool IsSimpleProperty(JProperty p)
        {
            return p.Value is JValue jValue && jValue.Type != JTokenType.Null;
        }

        const string SenderName = "mapping-names-sender";
        const string ReceiverName = "mapping-names-receiver";
        const string AuditName = "mapping-names-audit";
        const string DefaultConnectionStringName = "default_account";
        const string AnotherConnectionStringName = "another_account";

        class Context : ScenarioContext
        {
            public bool Received { get; set; }
            public Dictionary<string, string> AllPropertiesFlattened { get; set; }
            public string[] ContainingRawConnectionString { get; set; }
        }

        class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServer>(cfg =>
                {
                    var transport = cfg.ConfigureTransport<AzureStorageQueueTransport>();
                    transport.AccountRouting.DefaultAccountAlias = DefaultConnectionStringName;
                    transport.AccountRouting.AddAccount(AnotherConnectionStringName, new QueueServiceClient(Utilities.GetEnvConfiguredConnectionString2()), CloudStorageAccount.Parse(Utilities.GetEnvConfiguredConnectionString2()).CreateCloudTableClient());

                    cfg.UseSerialization<NewtonsoftJsonSerializer>();
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
                    cfg.UseSerialization<NewtonsoftJsonSerializer>();
                    cfg.AuditProcessedMessagesTo(AuditName);

                    transport.AccountRouting.DefaultAccountAlias = AnotherConnectionStringName;
                    transport.AccountRouting.AddAccount(DefaultConnectionStringName, new QueueServiceClient(Utilities.GetEnvConfiguredConnectionString()), CloudStorageAccount.Parse(Utilities.GetEnvConfiguredConnectionString()).CreateCloudTableClient());
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
                    cfg.UseSerialization<NewtonsoftJsonSerializer>();
                    cfg.AuditProcessedMessagesTo(AuditName);

                    var transport = cfg.ConfigureTransport<AzureStorageQueueTransport>();
                    transport.AccountRouting.DefaultAccountAlias = DefaultConnectionStringName;
                    transport.AccountRouting.AddAccount(AnotherConnectionStringName, new QueueServiceClient(Utilities.GetEnvConfiguredConnectionString2()), CloudStorageAccount.Parse(Utilities.GetEnvConfiguredConnectionString2()).CreateCloudTableClient());
                });
                CustomEndpointName(ReceiverName);
            }
        }

        class Handler : IHandleMessages<MyCommand>
        {
            public Handler(Context testContext)
            {
                this.testContext = testContext;
            }

            public Task Handle(MyCommand message, IMessageHandlerContext context)
            {
                testContext.Received = true;
                return Task.FromResult(0);
            }

            Context testContext;
        }

        public class MyCommand : ICommand
        {
        }
    }
}