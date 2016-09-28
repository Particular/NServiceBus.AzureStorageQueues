namespace NServiceBus.Azure.Transports.WindowsAzureStorageQueues.AcceptanceTests.Sending
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Microsoft.WindowsAzure.Storage;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;
    using NUnit.Framework.Compatibility;

    //TODO: should this be explicit
    //[Explicit("long running")]
    public class When_delaying_messages_natively : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_receive_the_message_after_delay()
        {
            var delay = TimeSpan.FromMinutes(1);

            var context = await Scenario.Define<Context>()
                .WithEndpoint<Sender>(b => b.When((session, c) =>
                {
                    var sendOptions = new SendOptions();
                    sendOptions.DelayDeliveryWith(delay);
                    c.SW = new Stopwatch();
                    c.SW.Start();
                    return session.Send(new MyMessage { Id = c.TestRunId }, sendOptions);
                }))
                .WithEndpoint<Receiver>()
                .Done(c => c.WasCalled)
                .Run(delay + TimeSpan.FromMinutes(1));

            Assert.True(context.WasCalled, "The message handler should be called");
            Assert.Greater(context.SW.Elapsed, delay);
        }

        [Test]
        public async Task Should_send_message_with_long_delay()
        {
            var delay = TimeSpan.FromDays(30);

            var context = await Scenario.Define<Context>()
                .WithEndpoint<Sender>(b => b.When(async (session, c) =>
                {
                    var sendOptions = new SendOptions();
                    sendOptions.DelayDeliveryWith(delay);
                    await session.Send(new MyMessage
                    {
                        Id = c.TestRunId
                    }, sendOptions).ConfigureAwait(false);
                    c.WasCalled = true;
                }))
                .WithEndpoint<Receiver>()
                .Done(c => c.WasCalled)
                .Run();
            Assert.True(context.WasCalled, "The message handler should be called");
        }

        [Test]
        public async Task Should_resend_message_if_delay_did_not_pass()
        {
            var delay = TimeSpan.FromMinutes(1);

            // remove the visibilitytimeout up to 5 times, which will make the message to be requeued again and again
            var countDown = new SemaphoreSlim(5);
            EventHandler<RequestEventArgs> h = (o, e) =>
            {
                var req = e.Request;

                if (req.Method == "POST")
                {
                    var values = req.Headers.GetValues("visibilitytimeout");
                    if (values != null && values.Length > 0)
                    {
                        if (countDown.Wait(TimeSpan.Zero))
                        {
                            req.Headers.Remove("visibilitytimeout");
                        }
                    }
                }
            };
            OperationContext.GlobalSendingRequest += h;
            try
            {

                var context = await Scenario.Define<Context>()
                    .WithEndpoint<Sender>(b => b.When(async (session, c) =>
                    {
                        var sendOptions = new SendOptions();
                        sendOptions.DelayDeliveryWith(delay);
                        await session.Send(new MyMessage
                        {
                            Id = c.TestRunId
                        }, sendOptions).ConfigureAwait(false);
                        c.WasCalled = true;
                    }))
                    .WithEndpoint<Receiver>()
                    .Done(c => c.WasCalled)
                    .Run(delay + TimeSpan.FromMinutes(1));
                Assert.True(context.WasCalled, "The message handler should be called");
            }
            finally
            {
                OperationContext.GlobalSendingRequest -= h;
            }
        }

        public class Context : ScenarioContext
        {
            public bool WasCalled { get; set; }
            public Stopwatch SW { get; set; }
        }

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServer>(cfg =>
                {
                    cfg.UseTransport<AzureStorageQueueTransport>().UseNativeTimeouts();
                }).AddMapping<MyMessage>(typeof(Receiver));
            }
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServer>();
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public Context TestContext { get; set; }

                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    if (TestContext.TestRunId != message.Id)
                    {
                        return Task.FromResult(0);
                    }

                    TestContext.WasCalled = true;

                    return Task.FromResult(0);
                }
            }
        }

        public class MyMessage : IMessage
        {
            public Guid Id { get; set; }
        }
    }
}