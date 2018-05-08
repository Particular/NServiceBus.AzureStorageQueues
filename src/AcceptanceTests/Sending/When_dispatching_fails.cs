namespace NServiceBus.AcceptanceTests.WindowsAzureStorageQueues.Sending
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using EndpointTemplates;
    using Microsoft.WindowsAzure.Storage;
    using NUnit.Framework;
    using LogLevel = Logging.LogLevel;

    public class When_dispatching_fails : NServiceBusAcceptanceTest
    {
        [Test]
        public void Should_log_send_related_error()
        {
            Assert.ThrowsAsync<StorageException>(() => Scenario.Define<Context>()
                .WithEndpoint<Sender>(b => b.When((bus, c) => Send(bus)))
                .WithEndpoint<Receiver>()
                .Done(c => c.Logs.Any(li => li.Message.Contains("Fail on proxy") && li.Level == LogLevel.Error))
                .Run());
        }

        [Test]
        public void Should_log_queue_related_error_if_queue_doesnt_exist()
        {
            const string queue = "non-existent";

            Assert.ThrowsAsync<Exception>(() => Scenario.Define<Context>()
                .WithEndpoint<Sender>(b => b.When((bus, c) =>
                {
                    var options = new SendOptions();
                    options.SetDestination(queue);
                    return bus.Send(new MyMessage(), options);
                }))
                .WithEndpoint<Receiver>()
                .Done(c => c.Logs.Any(li => li.Message.Contains($"The queue {queue} was not found. Create the queue.") && li.Level == LogLevel.Error))
                .Run());
        }

        static async Task Send(IMessageSession messageSession)
        {
            await messageSession.Send(new MyMessage()).ConfigureAwait(false);

            // https://github.com/Azure/azure-storage-net/issues/534
            EventHandler<RequestEventArgs> failRequests = delegate { throw new Exception("Fail on proxy"); };
            OperationContext.GlobalSendingRequest += failRequests;

            try
            {
                await messageSession.Send(new MyMessage()).ConfigureAwait(false);
            }
            finally
            {
                OperationContext.GlobalSendingRequest -= failRequests;
            }
        }

        public class Context : ScenarioContext
        {
        }

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServer>(cfg => cfg.ConfigureTransport().Routing().RouteToEndpoint(typeof(MyMessage), typeof(Receiver)));
            }
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServer>();
            }
        }

        public class MyMessage : IMessage
        {
        }

        public class MyMessageHandler : IHandleMessages<MyMessage>
        {
            public Task Handle(MyMessage message, IMessageHandlerContext context)
            {
                return Task.FromResult(0);
            }
        }
    }
}