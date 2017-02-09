namespace NServiceBus.Azure.Transports.WindowsAzureStorageQueues.AcceptanceTests.Sending
{
    using System;
    using System.Text;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Queue;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_message_is_sent_with_no_reply_to_header : NServiceBusAcceptanceTest
    {
        const string EndpointName = "custom-no-reply-receiver";
        CloudQueue helperQueue;
        CloudQueue testQueue;
        string connectionString;

        [SetUp]
        public new void SetUp()
        {
            connectionString = Environment.GetEnvironmentVariable("AzureStorageQueueTransport.ConnectionString");
            var account = CloudStorageAccount.Parse(connectionString);
            var queues = account.CreateCloudQueueClient();
            helperQueue = queues.GetQueueReference("custom-no-reply-header");
            testQueue = queues.GetQueueReference(EndpointName);

            helperQueue.CreateIfNotExists();
            testQueue.CreateIfNotExists();
        }

        [Test]
        public void Should_dispatch_properly()
        {
            var context = new Context();

            Scenario.Define(context)
                .WithEndpoint<Receiver>(b =>
                {
                    b.Given((bus, c) =>
                    {
                        bus.Send(new Address(helperQueue.Name, connectionString), new Message());

                        try
                        {
                            var msg = helperQueue.GetMessage();
                            helperQueue.DeleteMessage(msg);

                            var bodyWithNoReplyTo = GetBodyWithNoReplyTo(msg);
                            testQueue.AddMessage(new CloudQueueMessage(bodyWithNoReplyTo));
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e);
                            throw;
                        }
                    });
                })
                .Run();


            Assert.IsTrue(context.WasCalled);
        }

        [TearDown]
        public void TearDown()
        {
            helperQueue.Clear();
            testQueue.Clear();
        }

        static byte[] GetBodyWithNoReplyTo(CloudQueueMessage msg)
        {
            const string replytoaddress = "\"ReplyToAddress\":\"";
            const string closingValue = "\",";

            var rawBody = msg.AsString;
            var start = rawBody.IndexOf(replytoaddress);
            var end = rawBody.IndexOf(closingValue, start);
            var stringBody = rawBody.Replace(rawBody.Substring(start, end - start + closingValue.Length), "");
            return Encoding.UTF8.GetBytes(stringBody.ToCharArray());
        }

        public class Context : ScenarioContext
        {
            public bool WasCalled { get; set; }
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServer>().CustomEndpointName(EndpointName);
            }

            public class MyMessageHandler : IHandleMessages<Message>
            {
                public Context Context { get; set; }

                public IBus Bus { get; set; }

                public void Handle(Message message)
                {
                    Context.WasCalled = true;
                }
            }

        }

        public class Message : IMessage
        {
        }
    }
}
