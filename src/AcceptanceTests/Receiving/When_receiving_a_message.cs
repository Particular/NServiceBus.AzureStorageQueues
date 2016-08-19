namespace NServiceBus.Azure.Transports.WindowsAzureStorageQueues.AcceptanceTests.Receiving
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_receiving_a_message : NServiceBusAcceptanceTest
    {
        [Explicit("This is a test ensuring coherent behavior for long-lasting message processing. " +
                  "It should fail even if the Azure Storage Queues enable removing a message after a lease time out. ")]
        [TestCase(1)]
        [TestCase(2)]
        public async Task Should_expire_pop_receipt_disregarding_consumer_number(int concurrency)
        {
            var ctx = await Scenario.Define<Context>()
                .WithEndpoint<Receiver>(b =>
                {
                    b.DoNotFailOnErrorMessages();
                    b.CustomConfig(config => config.LimitMessageProcessingConcurrencyTo(concurrency));
                    b.When(async (bus, c) => { await bus.SendLocal(new MyMessage()); });
                })
                .Done(c => c.Logs.Any(IsPopReceiptLogItem))
                .Run();

            var items = ctx.Logs.Where(IsPopReceiptLogItem).ToArray();

            CollectionAssert.IsNotEmpty(items);
        }

        static bool IsPopReceiptLogItem(ScenarioContext.LogItem item)
        {
            return item.Message.Contains("Pop receipt is invalid as it exceeded the next visible time.");
        }

        const int VisibilityTimeoutInSeconds = 5;
        static readonly TimeSpan VisibilityTimeout = TimeSpan.FromSeconds(VisibilityTimeoutInSeconds);
        static readonly TimeSpan HandlingTimeout = TimeSpan.FromSeconds(VisibilityTimeoutInSeconds*4);

        class Context : ScenarioContext
        {
            public bool ShouldDelay()
            {
                return Interlocked.Exchange(ref signaled, 1) == 0;
            }

            int signaled;
        }

        class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServer>(config =>
                {
                    config.UseTransport<AzureStorageQueueTransport>()
                        .Transactions(TransportTransactionMode.ReceiveOnly)
                        .MessageInvisibleTime(VisibilityTimeout);
                });
            }
        }

        class MyMessage : IMessage
        {
        }

        class MyMessageHandler : IHandleMessages<MyMessage>
        {
            public MyMessageHandler(Context ctx)
            {
                this.ctx = ctx;
            }

            public async Task Handle(MyMessage message, IMessageHandlerContext context)
            {
                if (ctx.ShouldDelay())
                {
                    await Task.Delay(HandlingTimeout);
                }
            }

            readonly Context ctx;
        }
    }
}