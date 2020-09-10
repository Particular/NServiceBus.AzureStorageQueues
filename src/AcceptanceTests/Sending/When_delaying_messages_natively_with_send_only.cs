namespace NServiceBus.Azure.Transports.WindowsAzureStorageQueues.AcceptanceTests.Sending
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Table;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;
    using Testing;

    public class When_delaying_messages_natively_with_send_only : NServiceBusAcceptanceTest
    {
        [SetUp]
        public async Task SetUpLocal()
        {
            delayedMessagesTable = CloudStorageAccount.Parse(Utilities.GetEnvConfiguredConnectionString()).CreateCloudTableClient().GetTableReference(SenderDelayedMessagesTable);
            if (await delayedMessagesTable.ExistsAsync().ConfigureAwait(false))
            {
                foreach (var dte in await delayedMessagesTable.ExecuteQuerySegmentedAsync(new TableQuery(), null).ConfigureAwait(false))
                {
                    await delayedMessagesTable.ExecuteAsync(TableOperation.Delete(dte)).ConfigureAwait(false);
                }
            }
        }

        [Test]
        public async Task Should_receive_the_message_after_delay()
        {
            var delay = TimeSpan.FromSeconds(30);

            var context = await Scenario.Define<Context>()
                .WithEndpoint<Sender>(b => b.When((session, c) =>
                {
                    var sendOptions = new SendOptions();
                    sendOptions.DelayDeliveryWith(delay);
                    c.Stopwatch = Stopwatch.StartNew();
                    return session.Send(new MyMessage
                    {
                        Id = c.TestRunId
                    }, sendOptions);
                }))
                .WithEndpoint<Receiver>()
                .Done(c => c.WasCalled)
                .Run(delay + TimeSpan.FromMinutes(1)).ConfigureAwait(false);

            Assert.True(context.WasCalled, "The message handler should be called");
            Assert.Greater(context.Stopwatch.Elapsed, delay);
        }

        [Test]
        public async Task Should_send_message_to_error_queue_when_target_queue_does_not_exist()
        {
            var delay = TimeSpan.FromDays(30);

            var context = await Scenario.Define<Context>()
                .WithEndpoint<SenderToNowhere>(b => b.When(async (session, c) =>
                {
                    var sendOptions = new SendOptions();
                    sendOptions.DelayDeliveryWith(delay);
                    sendOptions.SetDestination("thisisnonexistingqueuename");
                    await session.Send(new MyMessage
                    {
                        Id = c.TestRunId
                    }, sendOptions).ConfigureAwait(false);

                    var delayedMessages = await GetDelayedMessageEntities().ConfigureAwait(false);
                    await MoveBeforeNow(delayedMessages[0]).ConfigureAwait(false);
                }))
                .WithEndpoint<Receiver>()
                .Done(c => c.WasCalled)
                .Run().ConfigureAwait(false);

            Assert.True(context.WasCalled, "The message should have been moved to the error queue");
        }

        async Task MoveBeforeNow(ITableEntity dte)
        {
            var earlier = DateTimeOffset.UtcNow - TimeSpan.FromMinutes(5);

            var ctx = new OperationContext();

            var delayedMessageEntity = new DynamicTableEntity();
            delayedMessageEntity.ReadEntity(dte.WriteEntity(ctx), ctx);

            delayedMessageEntity.PartitionKey = earlier.ToString("yyyyMMddHH");
            delayedMessageEntity.RowKey = earlier.ToString("yyyyMMddHHmmss");

            await delayedMessagesTable.ExecuteAsync(TableOperation.Delete(dte)).ConfigureAwait(false);
            await delayedMessagesTable.ExecuteAsync(TableOperation.Insert(delayedMessageEntity)).ConfigureAwait(false);
        }

        async Task<IList<DynamicTableEntity>> GetDelayedMessageEntities()
        {
            return (await delayedMessagesTable.ExecuteQuerySegmentedAsync(new TableQuery(), null).ConfigureAwait(false)).Results;
        }

        CloudTable delayedMessagesTable;

        const string SenderDelayedMessagesTable = "NativeDelayedMessagesForSenderSendOnly";

        public class Context : ScenarioContext
        {
            public bool WasCalled { get; set; }
            public Stopwatch Stopwatch { get; set; }
        }

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServer>(cfg =>
                {
                    cfg.SendOnly();

                    var transport = cfg.UseTransport<AzureStorageQueueTransport>();
                    transport.DelayedDelivery().UseTableName(SenderDelayedMessagesTable);
                    var routing = cfg.ConfigureTransport().Routing();
                    routing.RouteToEndpoint(typeof(MyMessage), typeof(Receiver));
                });
            }
        }

        public class SenderToNowhere : EndpointConfigurationBuilder
        {
            public SenderToNowhere()
            {
                EndpointSetup<DefaultServer>(cfg =>
                {
                    cfg.SendOnly();

                    var transport = cfg.UseTransport<AzureStorageQueueTransport>();
                    transport.DelayedDelivery().UseTableName(SenderDelayedMessagesTable);
                    cfg.SendFailedMessagesTo(Conventions.EndpointNamingConvention(typeof(Receiver)));
                });
            }
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServer>(cfg => { cfg.UseTransport<AzureStorageQueueTransport>(); });
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                Context testContext;

                public MyMessageHandler(Context testContext)
                {
                    this.testContext = testContext;
                }

                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    if (testContext.TestRunId != message.Id)
                    {
                        return Task.FromResult(0);
                    }

                    testContext.WasCalled = true;

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