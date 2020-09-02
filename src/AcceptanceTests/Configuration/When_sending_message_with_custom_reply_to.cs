namespace NServiceBus.AcceptanceTests.WindowsAzureStorageQueues.Configuration
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using EndpointTemplates;
    using global::Azure.Storage.Queues;
    using global::Azure.Storage.Queues.Models;
    using global::Newtonsoft.Json;
    using global::Newtonsoft.Json.Linq;
    using NUnit.Framework;
    using NUnit.Framework.Interfaces;

    public class When_sending_message_with_custom_reply_to : NServiceBusAcceptanceTest
    {
        static When_sending_message_with_custom_reply_to()
        {
            connectionString = Testing.Utilities.GetEnvConfiguredConnectionString();
            anotherConnectionString = ConfigureEndpointAzureStorageQueueTransport.AnotherConnectionString;
            RawReplyTo = "q@" + anotherConnectionString;
        }

        [TestCaseSource(nameof(GetTestCases))]
        public async Task Should_preserve_fully_qualified_name_when_using_mappings(string destination, string replyTo, string auditConnectionString)
        {
            await Run<SenderUsingNamesInsteadOfConnectionStrings>(destination, replyTo, auditConnectionString).ConfigureAwait(false);
        }

        [TestCaseSource(nameof(GetTestCases))]
        public async Task Should_preserve_fully_qualified_name_when_using_raw_connection_strings(string destination, string replyTo, string auditConnectionString)
        {
            await Run<SenderNotUsingNamesInsteadOfConnectionStrings>(destination, replyTo, auditConnectionString).ConfigureAwait(false);
        }

        async Task Run<TSender>(string destination, string replyTo, string auditConnectionString) where TSender : Sender
        {
            var auditQueue = new QueueClient(auditConnectionString, AuditName);
            await auditQueue.CreateIfNotExistsAsync();

            await Scenario.Define<Context>()
                .WithEndpoint<TSender>(b => b.When(async s => { await Send(s, replyTo, destination).ConfigureAwait(false); }))
                .Done(c => true)
                .Run().ConfigureAwait(false);

            QueueMessage[] messages = await auditQueue.ReceiveMessagesAsync(1).ConfigureAwait(false);
            var msg = messages[0];
            await auditQueue.DeleteMessageAsync(msg.MessageId, msg.PopReceipt).ConfigureAwait(false);

            AssertReplyTo(msg, replyTo);
        }

        static void AssertReplyTo(QueueMessage m1, string expectedReplyTo)
        {
            var bytes = Convert.FromBase64String(m1.MessageText);
            using (var reader = new JsonTextReader(new StreamReader(new MemoryStream(bytes))))
            {
                var token = JToken.ReadFrom(reader);
                var headers = token["Headers"];
                var replyTo = headers[Headers.ReplyToAddress];

                Assert.AreEqual(expectedReplyTo, ((JValue) replyTo).Value);
            }
        }

        static IEnumerable<ITestCaseData> GetTestCases()
        {
            // combinatorial
            yield return new TestCaseData(AuditName, RawReplyTo, connectionString);
            yield return new TestCaseData(AuditName, MappedReplyTo, connectionString);
            yield return new TestCaseData(AuditNameAtAnotherAccount, RawReplyTo, anotherConnectionString);
            yield return new TestCaseData(AuditNameAtAnotherAccount, MappedReplyTo, anotherConnectionString);
        }

        static async Task Send(IMessageSession s, string replyTo, string destination)
        {
            var o = new SendOptions();
            o.RouteReplyTo(replyTo);
            o.SetDestination(destination);
            await s.Send(new MyCommand(), o).ConfigureAwait(false);
        }

        const string AuditName = "custom-reply-to-destination";
        const string AuditNameAtAnotherAccount = AuditName + "@" + AnotherConnectionStringName;
        const string DefaultConnectionStringName = "default_account";
        const string AnotherConnectionStringName = "another_account";
        const string MappedReplyTo = "q@another_account";
        static readonly string RawReplyTo;

        static readonly string connectionString;
        static readonly string anotherConnectionString;

        class Context : ScenarioContext
        {
        }

        public abstract class Sender : EndpointConfigurationBuilder
        {
            protected Sender()
            {
                CustomEndpointName("custom-reply-to-sender");
                EndpointSetup<DefaultServer>(cfg =>
                {
                    cfg.UseSerialization<NewtonsoftSerializer>();

                    var transport = cfg.UseTransport<AzureStorageQueueTransport>()
                        .ConnectionString(() => connectionString);

                    SetUp(transport);

                    transport
                        .DefaultAccountAlias(DefaultConnectionStringName)
                        .AccountRouting()
                        .AddAccount(AnotherConnectionStringName, anotherConnectionString);
                });
            }

            protected abstract void SetUp(TransportExtensions<AzureStorageQueueTransport> transport);
        }

        public class SenderUsingNamesInsteadOfConnectionStrings : Sender
        {
            protected override void SetUp(TransportExtensions<AzureStorageQueueTransport> transport)
            {
                transport.UseAccountAliasesInsteadOfConnectionStrings();
            }
        }

        public class SenderNotUsingNamesInsteadOfConnectionStrings : Sender
        {
            protected override void SetUp(TransportExtensions<AzureStorageQueueTransport> transport)
            {
            }
        }

        [Serializable]
        public class MyCommand : ICommand
        {
        }
    }
}