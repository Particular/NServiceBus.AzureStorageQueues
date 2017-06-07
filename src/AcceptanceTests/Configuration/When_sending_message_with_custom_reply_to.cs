namespace NServiceBus.AcceptanceTests.WindowsAzureStorageQueues.Configuration
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Azure.Transports.WindowsAzureStorageQueues.AcceptanceTests;
    using EndpointTemplates;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Queue;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using NUnit.Framework;
    using NUnit.Framework.Interfaces;
    using Serializer = JsonSerializer;

    public class When_sending_message_with_custom_reply_to : NServiceBusAcceptanceTest
    {
        static When_sending_message_with_custom_reply_to()
        {
            connectionString = Utils.GetEnvConfiguredConnectionString();
            anotherConnectionString = Utils.BuildAnotherConnectionString(connectionString);
            RawReplyTo = "q@" + anotherConnectionString;
        }

        public When_sending_message_with_custom_reply_to()
        {
            var account = CloudStorageAccount.Parse(connectionString);
            auditQueue = account.CreateCloudQueueClient().GetQueueReference(AuditName);
        }

        [OneTimeSetUp]
        public Task OneTimeSetup()
        {
            return auditQueue.CreateIfNotExistsAsync();
        }

        [TestCaseSource(nameof(GetTestCases))]
        public async Task Should_preserve_fully_qualified_name_when_using_mappings(string destination, string replyTo)
        {
            await Run<SenderUsingNamesInsteadOfConnectionStrings>(destination, replyTo).ConfigureAwait(false);
        }

        [TestCaseSource(nameof(GetTestCases))]
        public async Task Should_preserve_fully_qualified_name_when_using_raw_connection_strings(string destination, string replyTo)
        {
            await Run<SenderNotUsingNamesInsteadOfConnectionStrings>(destination, replyTo).ConfigureAwait(false);
        }

        async Task Run<TSender>(string destination, string replyTo) where TSender : Sender
        {
            await Scenario.Define<Context>()
                .WithEndpoint<TSender>(b => b.When(async s => { await Send(s, replyTo, destination).ConfigureAwait(false); }))
                .Done(c => true)
                .Run().ConfigureAwait(false);

            var msg = await auditQueue.GetMessageAsync().ConfigureAwait(false);
            await auditQueue.DeleteMessageAsync(msg).ConfigureAwait(false);

            AssertReplyTo(msg, replyTo);
        }

        static void AssertReplyTo(CloudQueueMessage m1, string expectedReplyTo)
        {
            using (var reader = new JsonTextReader(new StreamReader(new MemoryStream(m1.AsBytes))))
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
            yield return new TestCaseData(AuditName, RawReplyTo);
            yield return new TestCaseData(AuditName, MappedReplyTo);
            yield return new TestCaseData(AuditNameAtAnotherAccount, RawReplyTo);
            yield return new TestCaseData(AuditNameAtAnotherAccount, MappedReplyTo);
        }

        static async Task Send(IMessageSession s, string replyTo, string destination)
        {
            var o = new SendOptions();
            o.RouteReplyTo(replyTo);
            o.SetDestination(destination);
            await s.Send(new MyCommand(), o).ConfigureAwait(false);
        }

        readonly CloudQueue auditQueue;
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
                    cfg.UseSerialization<Serializer>();

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