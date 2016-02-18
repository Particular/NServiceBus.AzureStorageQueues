namespace NServiceBus.Azure.Transports.WindowsAzureStorageQueues.AcceptanceTests.Sending
{
    using System;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NServiceBus.AcceptanceTests.ScenarioDescriptors;
    using NUnit.Framework;

    public class When_dispatching_fails : NServiceBusAcceptanceTest
    {
        [Test]
        public void Should_log_send_related_error()
        {
            Assert.Throws<AggregateException>(async () => await Scenario.Define<Context>()
                .WithEndpoint<Sender>(b => b.When((bus, c) => Send(bus)))
                .WithEndpoint<Receiver>()
                .Done(c => c.Logs.Any(li => li.Message.Contains(UnableToDispatchException.ExceptionMessage) && li.Level == "error"))
                .Repeat(r => r.For(Transports.Default))
                .Run());
        }

        [Test]
        public void Should_log_queue_related_error_if_queue_doesnt_exist()
        {
            const string queue = "non-existent";

            Assert.Throws<AggregateException>(async () => await Scenario.Define<Context>()
                .WithEndpoint<Sender>(b => b.When((bus, c) =>
                {
                    var options = new SendOptions();
                    options.SetDestination(queue);
                    return bus.Send(new MyMessage(), options);
                }))
                .WithEndpoint<Receiver>()
                .Done(c => c.Logs.Any(li => li.Message.Contains($"The queue {queue} was not found. Create the queue.") && li.Level == "error"))
                .Repeat(r => r.For(Transports.Default))
                .Run());
        }

        private static async Task Send(IBusSession bus)
        {
            await bus.Send(new MyMessage()).ConfigureAwait(false);
            var prev = WebRequest.DefaultWebProxy;
            WebRequest.DefaultWebProxy = new ThrowingProxy();
            try
            {
                await bus.Send(new MyMessage());
            }
            finally
            {
                WebRequest.DefaultWebProxy = prev;
            }
        }

        private class ThrowingProxy : IWebProxy
        {
            public readonly static Exception ThrownException = new Exception("Fail on proxy");

            public Uri GetProxy(Uri destination)
            {
                throw ThrownException;
            }

            public bool IsBypassed(Uri host)
            {
                throw ThrownException;
            }

            public ICredentials Credentials { get; set; }
        }

        public class Context : ScenarioContext
        {
        }

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServer>()
                    .AddMapping<MyMessage>(typeof(Receiver));
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