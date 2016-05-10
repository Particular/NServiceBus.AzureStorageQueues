namespace NServiceBus.AcceptanceTests.WindowsAzureStorageQueues.Configuration
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using EndpointTemplates;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Queue;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using NUnit.Framework;

    public class When_mapping_account_names : NServiceBusAcceptanceTest
    {
        readonly CloudQueue auditQueue;
        static string defaultConnectionString;
        static string anotherConnectionString;
        const string SenderName = "mapping-names-sender";
        const string ReceiverName = "mapping-names-receiver";
        const string AuditName = "mapping-names-audit";
        const string DefaultConnectionStringName = "default_account";
        const string AnotherConnectionStringName = "another_account";

        public When_mapping_account_names()
        {
            defaultConnectionString = Environment.GetEnvironmentVariable("AzureStorageQueueTransport.ConnectionString");
            anotherConnectionString = defaultConnectionString + ";BlobEndpoint=https://notusedatall.blob.core.windows.net";

            var account = CloudStorageAccount.Parse(defaultConnectionString);
            auditQueue = account.CreateCloudQueueClient().GetQueueReference(AuditName);
        }

        [Test]
        public async Task Is_disabled_Should_audit_with_raw_connection_strings()
        {
            var ctx = await SendMessage<ReceiverUsingRawConnectionStrings>(ReceiverName);

            CollectionAssert.IsNotEmpty(ctx.ContainingRawConnectionString);
            foreach (var name in ctx.ContainingRawConnectionString)
            {
                Assert.True(name.Contains("ReplyToAddress"), $"'{name}' should have been reply-to-address");
            }
        }

        [Test]
        public async Task Is_enabled_and_single_account_is_used_Should_audit_just_queue_name_without_account()
        {
            var ctx = await SendMessage<ReceiverUsingMappedConnectionStrings>(ReceiverName);
            CollectionAssert.IsEmpty(ctx.ContainingRawConnectionString, "Message headers should not include raw connection string");

            foreach (var propertyWithSenderName in ctx.AllPropertiesFlattened.Where(property => property.Value.Contains(SenderName)))
            {
                Assert.AreEqual(SenderName, propertyWithSenderName.Value);
            }
        }

        [Test]
        public async Task Is_enabled_and_sending_to_another_account_Should_audit_fully_qualified_queue()
        {
            var ctx = await SendMessage<ReceiverUsingMappedConnectionStrings>(ReceiverName + "@" + AnotherConnectionStringName);
            CollectionAssert.IsEmpty(ctx.ContainingRawConnectionString, "Message headers should not include raw connection string");

            var exluded = new HashSet<string>
            {
                Headers.OriginatingEndpoint
            };

            foreach (var propertyWithSenderName in ctx.AllPropertiesFlattened.Where(property => property.Value.Contains(SenderName)))
            {
                if (exluded.Contains(propertyWithSenderName.Key))
                {
                    continue;
                }

                const string expected = SenderName + "@" + DefaultConnectionStringName;
                Assert.AreEqual(expected, propertyWithSenderName.Value, propertyWithSenderName.Key);
            }
        }

        async Task<Context> SendMessage<TReceiver>(string destination)
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
                            .Run();

            Assert.IsTrue(ctx.Received);

            var rawMessage = await auditQueue.PeekMessageAsync();
            JToken message;
            using (var reader = new JsonTextReader(new StreamReader(new MemoryStream(rawMessage.AsBytes))))
            {
                message = JToken.ReadFrom(reader);
            }

            ctx.AllPropertiesFlattened = message.FindProperties(IsSimpleProperty)
                .ToDictionary(jp => jp.Name, jp => ((JValue)jp.Value).Value<string>());

            ctx.ContainingRawConnectionString = ctx.AllPropertiesFlattened.Where(kvp => kvp.Value.Contains(defaultConnectionString))
                .Select(kvp => kvp.Key).ToArray();

            return ctx;
        }

        bool IsSimpleProperty(JProperty p)
        {
            var jValue = p.Value as JValue;
            if (jValue == null || jValue.Type == JTokenType.Null)
            {
                return false;
            }

            return true;
        }

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
                CustomEndpointName(SenderName);
                EndpointSetup<DefaultServer>(cfg =>
                {
                    cfg.UseTransport<AzureStorageQueueTransport>()
                        .UseAccountNamesInsteadOfConnectionStrings(DefaultConnectionStringName, mapping => mapping.MapAccount(AnotherConnectionStringName, anotherConnectionString));
                });
            }
        }

        abstract class Receiver : EndpointConfigurationBuilder
        {
            protected Receiver()
            {
                EndpointSetup<DefaultPublisher>(cfg =>
                {
                    var extensions = cfg.UseTransport<AzureStorageQueueTransport>()
                        .ConnectionString(anotherConnectionString);

                    Setup(extensions);
                });
                CustomEndpointName(ReceiverName);
                AuditTo(AuditName);
            }

            protected abstract void Setup(TransportExtensions<AzureStorageQueueTransport> cfg);
        }

        class ReceiverUsingRawConnectionStrings : Receiver
        {
            protected override void Setup(TransportExtensions<AzureStorageQueueTransport> cfg)
            {
            }
        }

        class ReceiverUsingMappedConnectionStrings : Receiver
        {
            protected override void Setup(TransportExtensions<AzureStorageQueueTransport> cfg)
            {
                cfg.UseAccountNamesInsteadOfConnectionStrings(AnotherConnectionStringName, mapping => mapping.MapAccount(DefaultConnectionStringName, defaultConnectionString));
            }
        }

        class Handler : IHandleMessages<MyCommand>
        {
            public Handler(Context testContext)
            {
                this.testContext = testContext;
            }

            readonly Context testContext;

            public Task Handle(MyCommand message, IMessageHandlerContext context)
            {
                testContext.Received = true;
                return Task.FromResult(0);
            }
        }

        class MyCommand : ICommand
        {
        }
    }
}