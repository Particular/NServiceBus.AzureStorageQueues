namespace NServiceBus.AzureStorageQueues.AcceptanceTests.Sending
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_deferring_a_message_natively_and_delayed_delivery_is_disabled : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_throw()
        {
            try
            {
                var context = await Scenario.Define<Context>()
                    .WithEndpoint<Endpoint>(b => b.When((session, ctx) =>
                    {
                        var delay = TimeSpan.FromSeconds(2);

                        var options = new SendOptions();

                        options.DelayDeliveryWith(delay);
                        options.RouteToThisEndpoint();

                        try
                        {
                            return session.Send(new MyMessage(), options);
                        }
                        catch (Exception exception)
                        {
                            ctx.SendException = exception;
                            return Task.FromResult(0);
                        }
                    }))
                    .Done(ctx => ctx.WasCalled)
                    .Run();

                Assert.IsFalse(context.WasCalled, "Endpoint's handler should never be invoked.");
            }
            catch (Exception exception)
            {
                Assert.True(exception.Message.Contains("Cannot delay delivery of messages when TimeoutManager is disabled or there is no infrastructure support for delayed messages"));
            }
        }

        public class Context : ScenarioContext
        {
            public bool WasCalled { get; set; }
            public Exception SendException { get; set; }
        }

        public class Endpoint : EndpointConfigurationBuilder
        {
            public Endpoint()
            {
                EndpointSetup<DefaultServer>(config =>
                {
                    var delayedDelivery = config.UseTransport<AzureStorageQueueTransport>().DelayedDelivery();
                    delayedDelivery.DisableTimeoutManager();
                    delayedDelivery.DisableDelayedDelivery();
                });
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public Context Context { get; set; }

                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    Context.WasCalled = true;
                    return Task.FromResult(0);
                }
            }
        }

        public class MyMessage : IMessage
        {
        }
    }
}