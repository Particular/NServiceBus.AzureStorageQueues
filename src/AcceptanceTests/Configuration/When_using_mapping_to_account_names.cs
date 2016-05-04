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

    public class When_using_mapping_to_account_names : NServiceBusAcceptanceTest
    {
        readonly CloudQueue auditQueue;
        readonly string connectionString;
        const string SenderName = "mapping-names-sender";
        const string ReceiverName = "mapping-names-receiver";
        const string AuditName = "mapping-names-audit";

        public When_using_mapping_to_account_names()
        {
            connectionString = Environment.GetEnvironmentVariable("AzureStorageQueueTransport.ConnectionString");
            var account = CloudStorageAccount.Parse(connectionString);
            auditQueue = account.CreateCloudQueueClient().GetQueueReference(AuditName);
        }

        [Test]
        public async Task Should_send_just_with_queue_name_for_same_account_without_names_mapping()
        {
            await SendMessage<SenderUsingConnectionStrings>();
        }

        [Test]
        public async Task Should_send_just_with_queue_name_for_same_account_with_names_mapping()
        {
            await SendMessage<SenderUsingNamesInsteadConnectionStrings>();
        }

        async Task<Context> SendMessage<TEndpointConfigurationBuilder>() 
            where TEndpointConfigurationBuilder : EndpointConfigurationBuilder
        {
            var ctx = await Scenario.Define<Context>()
                            .WithEndpoint<TEndpointConfigurationBuilder>(b => b.When(s =>
                            {
                                var options = new SendOptions();
                                options.RouteReplyTo(SenderName);
                                options.SetDestination(ReceiverName);
                                return s.Send(new MyCommand(), options);
                            }))
                            .WithEndpoint<Receiver>()
                            .Done(c => c.Received)
                            .Run();

            Assert.IsTrue(ctx.Received);

            var rawMessage = await auditQueue.PeekMessageAsync();
            JToken message;
            using (var reader = new JsonTextReader(new StreamReader(new MemoryStream(rawMessage.AsBytes))))
            {
                message = JToken.ReadFrom(reader);
            }

            ctx.PropertiesHavingRawConnectionString = message.FindProperties(HasRawConnectionString)
                .Select(jp => jp.Name)
                .ToList();

            CollectionAssert.IsEmpty(ctx.PropertiesHavingRawConnectionString,
                "Message headers should not include raw connection string");

            return ctx;
        }

        bool HasRawConnectionString(JProperty p)
        {
            var jValue = p.Value as JValue;
            if (jValue == null || jValue.Type == JTokenType.Null)
            {
                return false;
            }

            return jValue.Value<string>().Contains(connectionString);
        }

        class Context : ScenarioContext
        {
            public bool Received { get; set; }
            public List<string> PropertiesHavingRawConnectionString { get; set; }
        }

        class SenderUsingNamesInsteadConnectionStrings : EndpointConfigurationBuilder
        {
            public SenderUsingNamesInsteadConnectionStrings()
            {
                EndpointSetup<DefaultServer>(cfg =>
                {
                    cfg.UseTransport<AzureStorageQueueTransport>()
                        .Addressing().UseAccountNamesInsteadOfConnectionStrings();
                });
            }
        }

        class SenderUsingConnectionStrings : EndpointConfigurationBuilder
        {
            public SenderUsingConnectionStrings()
            {
                EndpointSetup<DefaultServer>(cfg =>
                {
                    cfg.UseTransport<AzureStorageQueueTransport>();
                });
            }
        }

        class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultPublisher>();
                CustomEndpointName(ReceiverName);
                AuditTo(AuditName);
            }

            public class Handler : IHandleMessages<MyCommand>
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
        }

        class MyCommand : ICommand
        {
        }
    }
}