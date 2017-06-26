namespace NServiceBus.AzureStorageQueues.AcceptanceTests.Sending
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Features;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;
    using AcceptanceTesting.Customization;
    using Configuration.AdvanceExtensibility;

    public class When_TimeoutManager_disabled : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_startup_properly_and_send_regular_messages()
        {
            var context = await Scenario.Define<Context>()
                .WithEndpoint<Sender>(b => b.When((session, c) => session.Send(new MyMessage { Id = c.TestRunId })))
                .WithEndpoint<Receiver>()
                .Done(c => c.WasCalled)
                .Run()
                .ConfigureAwait(false);

            Assert.True(context.WasCalled, "The message handler should be called");
        }

       
        public class Context : ScenarioContext
        {
            public bool WasCalled { get; set; }
        }

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServer>(cfg =>
                {
                    cfg.UseTransport<AzureStorageQueueTransport>();
                    cfg.DisableFeature<TimeoutManager>();
                    cfg.ConfigureTransport().Routing()
                        .RouteToEndpoint(typeof(MyMessage), typeof(Receiver));
                });
            }
        }
        
        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServer>(cfg =>
                {
                    cfg.UseTransport<AzureStorageQueueTransport>();
                    cfg.DisableFeature<TimeoutManager>();
                    
                    // override delayed delivery settings, to make them unset
                    cfg.GetSettings().Set<DelayedDeliverySettings>(new DelayedDeliverySettings());
                });
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