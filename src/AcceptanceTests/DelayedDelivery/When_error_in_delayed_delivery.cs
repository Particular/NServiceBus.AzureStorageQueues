namespace NServiceBus.Transport.AzureStorageQueues.AcceptanceTests.DelayedDelivery
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    class When_error_in_delayed_delivery : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_attach_exception_headers()
        {
            var ctx = await RunScenario();

            Assert.That(ctx.Headers, Is.Not.Null);
            Assert.That(ctx.Headers.Keys, Has.Member("NServiceBus.ExceptionInfo.ExceptionType"));
            Assert.That(ctx.Headers.Keys, Has.Member("NServiceBus.ExceptionInfo.HelpLink"));
            Assert.That(ctx.Headers.Keys, Has.Member("NServiceBus.ExceptionInfo.Message"));
            Assert.That(ctx.Headers.Keys, Has.Member("NServiceBus.ExceptionInfo.Source"));
            Assert.That(ctx.Headers.Keys, Has.Member("NServiceBus.ExceptionInfo.StackTrace"));
            Assert.That(ctx.Headers.Keys, Has.Member("NServiceBus.TimeOfFailure"));
        }

        [Test]
        public async Task Should_attach_failed_Q_header()
        {
            var ctx = await RunScenario();

            Assert.That(ctx.Headers, Is.Not.Null);
            Assert.That(ctx.Headers.Keys, Has.Member("NServiceBus.FailedQ"));
            Assert.That(ctx.Headers["NServiceBus.FailedQ"], Is.EqualTo("notexist"));
        }

        Task<MyContext> RunScenario(CancellationToken cancellationToken = default) => Scenario.Define<MyContext>()
            .WithEndpoint<SampleEndpoint>(endpoint => endpoint
                .DoNotFailOnErrorMessages()
                .When(session =>
                {
                    var sendOptions = new SendOptions();
                    sendOptions.SetDestination("notexist");
                    sendOptions.DelayDeliveryWith(TimeSpan.FromSeconds(2));
                    return session.Send(new MyMessage(), sendOptions, cancellationToken);
                })
            )
            .WithEndpoint<ErrorQueueSpy>()
            .Done(context => !cancellationToken.IsCancellationRequested && context.IsDone)
            .Run();

        class SampleEndpoint : EndpointConfigurationBuilder
        {
            public SampleEndpoint() =>
                EndpointSetup<DefaultServer>(
                    cfg =>
                    {
                        cfg.SendFailedMessagesTo("error");
                        cfg.Recoverability()
                            .Delayed(delayed => delayed.NumberOfRetries(0))
                            .Immediate(immediate => immediate.NumberOfRetries(0));
                    });

            class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public Task Handle(MyMessage message, IMessageHandlerContext context) => throw new NotImplementedException();
            }
        }

        class ErrorQueueSpy : EndpointConfigurationBuilder
        {
            public ErrorQueueSpy() =>
                EndpointSetup<DefaultServer>()
                    .CustomEndpointName("error");

            class MyMessageHandler : IHandleMessages<MyMessage>
            {
                MyContext scenarioContext;

                public MyMessageHandler(MyContext scenarioContext) => this.scenarioContext = scenarioContext;

                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    scenarioContext.Headers = context.MessageHeaders;
                    scenarioContext.IsDone = true;
                    return Task.CompletedTask;
                }
            }
        }

        class MyMessage : IMessage
        {
        }

        class MyContext : ScenarioContext
        {
            public bool IsDone { get; set; }
            public IReadOnlyDictionary<string, string> Headers { get; set; }
        }
    }
}
