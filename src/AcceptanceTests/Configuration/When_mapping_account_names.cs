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
        readonly string connectionString;
        const string SenderName = "mapping-names-sender";
        const string ReceiverName = "mapping-names-receiver";
        const string AuditName = "mapping-names-audit";

        public When_mapping_account_names()
        {
            connectionString = Environment.GetEnvironmentVariable("AzureStorageQueueTransport.ConnectionString");
            var account = CloudStorageAccount.Parse(connectionString);
            auditQueue = account.CreateCloudQueueClient().GetQueueReference(AuditName);
        }

        [Test]
        public async Task Is_disabled_Should_audit_with_raw_connection_strings()
        {
            var ctx = await SendMessage<ReceiverUsingRawConnectionStrings>();

            CollectionAssert.IsNotEmpty(ctx.ContainingRawConnectionString);
            foreach (var name in ctx.ContainingRawConnectionString)
            {
                Assert.True(name.Contains("ReplyToAddress"), $"'{name}' should have been reply-to-address");
            }
        }

        [Test]
        public async Task Is_enabled_and_single_account_is_used_Should_audit_just_queue_name_without_account()
        {
            var ctx = await SendMessage<ReceiverUsingMappedConnectionStrings>();
            CollectionAssert.IsEmpty(ctx.ContainingRawConnectionString, "Message headers should not include raw connection string");

            foreach (var propertyWithSenderName in ctx.AllPropertiesFlattened.Where(property => property.Value.Contains(SenderName)))
            {
                Assert.AreEqual(SenderName, propertyWithSenderName.Value);
            }
        }

        async Task<Context> SendMessage<TReceiver>()
            where TReceiver : EndpointConfigurationBuilder
        {
            var ctx = await Scenario.Define<Context>()
                            .WithEndpoint<Sender>(b => b.When(s =>
                            {
                                var options = new SendOptions();
                                options.RouteReplyTo(SenderName);
                                options.SetDestination(ReceiverName);
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

            ctx.ContainingRawConnectionString = ctx.AllPropertiesFlattened.Where(kvp => kvp.Value.Contains(connectionString))
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
                EndpointSetup<DefaultServer>();
            }
        }

        abstract class Receiver : EndpointConfigurationBuilder
        {
            protected Receiver()
            {
                EndpointSetup<DefaultPublisher>(Setup);
                CustomEndpointName(ReceiverName);
                AuditTo(AuditName);
            }

            protected abstract void Setup(EndpointConfiguration cfg);
        }

        class ReceiverUsingRawConnectionStrings : Receiver
        {
            protected override void Setup(EndpointConfiguration cfg)
            {
            }
        }

        class ReceiverUsingMappedConnectionStrings : Receiver
        {
            protected override void Setup(EndpointConfiguration cfg)
            {
                cfg.UseTransport<AzureStorageQueueTransport>()
                    .Addressing()
                    .UseAccountNamesInsteadOfConnectionStrings();
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