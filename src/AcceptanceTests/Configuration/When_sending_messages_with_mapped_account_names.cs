namespace NServiceBus.AcceptanceTests.WindowsAzureStorageQueues.Configuration
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using EndpointTemplates;
    using global::Newtonsoft.Json;
    using global::Newtonsoft.Json.Linq;
    using Microsoft.WindowsAzure.Storage;
    using NUnit.Framework;

    public class When_sending_messages_with_mapped_account_names : NServiceBusAcceptanceTest
    {
        public When_sending_messages_with_mapped_account_names()
        {
            defaultConnectionString = ConfigureEndpointAzureStorageQueueTransport.ConnectionString;
            anotherConnectionString = ConfigureEndpointAzureStorageQueueTransport.AnotherConnectionString;
        }

        [Test]
        public async Task Is_disabled_Should_audit_with_raw_connection_strings()
        {
            var ctx = await SendMessage<ReceiverUsingRawConnectionStrings>(ReceiverName, defaultConnectionString).ConfigureAwait(false);

            CollectionAssert.IsNotEmpty(ctx.ContainingRawConnectionString);
            foreach (var name in ctx.ContainingRawConnectionString)
            {
                Assert.True(name.Contains("ReplyToAddress"), $"'{name}' should have been reply-to-address");
            }
        }

        [Test]
        public async Task Is_enabled_and_single_account_is_used_Should_audit_just_queue_name_without_account()
        {
            var ctx = await SendMessage<ReceiverUsingOneMappedConnectionString>(ReceiverName, defaultConnectionString).ConfigureAwait(false);
            CollectionAssert.IsEmpty(ctx.ContainingRawConnectionString, "Message headers should not include raw connection string");

            foreach (var propertyWithSenderName in ctx.AllPropertiesFlattened.Where(property => property.Value.Contains(SenderName)))
            {
                Assert.AreEqual(SenderName, propertyWithSenderName.Value);
            }
        }

        [Test]
        public async Task Is_enabled_and_sending_to_another_account_Should_audit_fully_qualified_queue()
        {
            var ctx = await SendMessage<ReceiverUsingMappedConnectionStrings>($"{ReceiverName}@{AnotherConnectionStringName}", anotherConnectionString).ConfigureAwait(false);
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

        async Task<Context> SendMessage<TReceiver>(string destination, string destinationConnectionString)
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
                var account = CloudStorageAccount.Parse(destinationConnectionString);
                var receiverAuditQueue = account.CreateCloudQueueClient().GetQueueReference(AuditName);

                var rawMessage = await receiverAuditQueue.GetMessageAsync().ConfigureAwait(false);
                await receiverAuditQueue.DeleteMessageAsync(rawMessage).ConfigureAwait(false);
                if (rawMessage == null)
                {
                    Assert.Fail("No message in the audit queue to pick up.");
                }

                JToken message;
                using (var reader = new JsonTextReader(new StreamReader(new MemoryStream(rawMessage.AsBytes))))
                {
                    message = JToken.ReadFrom(reader);
                }

                propertiesFlattened = message.FindProperties(IsSimpleProperty)
                    .ToDictionary(jp => jp.Name, jp => ((JValue)jp.Value).Value<string>());

                if (propertiesFlattened.ContainsValue(ctx.TestRunId.ToString()))
                {
                    break;
                }
            } while (true);


            ctx.AllPropertiesFlattened = propertiesFlattened;

            ctx.ContainingRawConnectionString = ctx.AllPropertiesFlattened.Where(kvp => kvp.Value.Contains(defaultConnectionString))
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
        static string defaultConnectionString;
        static string anotherConnectionString;

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
                    cfg.UseSerialization<NewtonsoftSerializer>();
                    cfg.UseTransport<AzureStorageQueueTransport>()
                        .UseAccountAliasesInsteadOfConnectionStrings()
                        .DefaultAccountAlias(DefaultConnectionStringName)
                        .AccountRouting()
                        .AddAccount(AnotherConnectionStringName, anotherConnectionString);
                });
                CustomEndpointName(SenderName);
            }
        }

        abstract class Receiver : EndpointConfigurationBuilder
        {
            protected Receiver()
            {
                EndpointSetup<DefaultPublisher>(cfg =>
                {
                    cfg.UseSerialization<NewtonsoftSerializer>();
                    var extensions = cfg.UseTransport<AzureStorageQueueTransport>();

                    Setup(extensions);
                    cfg.AuditProcessedMessagesTo(AuditName);
                });
                CustomEndpointName(ReceiverName);
            }

            protected abstract void Setup(TransportExtensions<AzureStorageQueueTransport> cfg);
        }

        class ReceiverUsingRawConnectionStrings : Receiver
        {
            protected override void Setup(TransportExtensions<AzureStorageQueueTransport> cfg)
            {
                cfg.ConnectionString(defaultConnectionString);
            }
        }

        class ReceiverUsingMappedConnectionStrings : Receiver
        {
            protected override void Setup(TransportExtensions<AzureStorageQueueTransport> cfg)
            {
                cfg.ConnectionString(anotherConnectionString);

                cfg.UseAccountAliasesInsteadOfConnectionStrings()
                    .DefaultAccountAlias(AnotherConnectionStringName)
                    .AccountRouting()
                    .AddAccount(DefaultConnectionStringName, defaultConnectionString);
            }
        }
        class ReceiverUsingOneMappedConnectionString : Receiver
        {
            protected override void Setup(TransportExtensions<AzureStorageQueueTransport> cfg)
            {
                cfg.ConnectionString(defaultConnectionString);

                cfg.UseAccountAliasesInsteadOfConnectionStrings()
                    .DefaultAccountAlias(DefaultConnectionStringName)
                    .AccountRouting()
                    .AddAccount(AnotherConnectionStringName, anotherConnectionString);
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

            readonly Context testContext;
        }

        public class MyCommand : ICommand
        {
        }
    }
}