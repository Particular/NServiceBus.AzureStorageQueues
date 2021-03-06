namespace NServiceBus.Transport.AzureStorageQueues.AcceptanceTests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using Microsoft.Azure.Cosmos.Table;
    using NServiceBus.AcceptanceTests;
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
        public async Task Should_send_message_to_error_queue_when_target_queue_does_not_exist()
        {
            var delay = TimeSpan.FromDays(30);

            var context = await Scenario.Define<Context>()
                .WithEndpoint<SendOnlySenderToNowhere>(b => b.When(async (session, c) =>
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
                .WithEndpoint<ErrorQueueReceiver>()
                .Done(c => c.WasCalled)
                .Run().ConfigureAwait(false);

            Assert.True(context.WasCalled, "The message should have been moved to the error queue");
        }

        async Task MoveBeforeNow(ITableEntity dte, CancellationToken cancellationToken = default)
        {
            var earlier = DateTimeOffset.UtcNow - TimeSpan.FromMinutes(5);

            var ctx = new OperationContext();

            var delayedMessageEntity = new DynamicTableEntity();
            delayedMessageEntity.ReadEntity(dte.WriteEntity(ctx), ctx);

            delayedMessageEntity.PartitionKey = earlier.ToString("yyyyMMddHH");
            delayedMessageEntity.RowKey = earlier.ToString("yyyyMMddHHmmss");

            await delayedMessagesTable.ExecuteAsync(TableOperation.Delete(dte), cancellationToken).ConfigureAwait(false);
            await delayedMessagesTable.ExecuteAsync(TableOperation.Insert(delayedMessageEntity), cancellationToken).ConfigureAwait(false);
        }

        async Task<IList<DynamicTableEntity>> GetDelayedMessageEntities(CancellationToken cancellationToken = default)
        {
            return (await delayedMessagesTable.ExecuteQuerySegmentedAsync(new TableQuery(), null, cancellationToken).ConfigureAwait(false)).Results;
        }

        CloudTable delayedMessagesTable;

        const string SenderDelayedMessagesTable = "NativeDelayedMessagesForSenderSendOnly";

        public class Context : ScenarioContext
        {
            public bool WasCalled { get; set; }
            public Stopwatch Stopwatch { get; set; }
        }

        public class SendOnlySenderToNowhere : EndpointConfigurationBuilder
        {
            public SendOnlySenderToNowhere()
            {
                var transport = Utilities.CreateTransportWithDefaultTestsConfiguration(Utilities.GetEnvConfiguredConnectionString());
                transport.DelayedDelivery.DelayedDeliveryTableName = SenderDelayedMessagesTable;
                transport.DelayedDelivery.DelayedDeliveryPoisonQueue = Conventions.EndpointNamingConvention(typeof(ErrorQueueReceiver));

                EndpointSetup(new CustomizedServer(transport), (cfg, rd) =>
                {
                    cfg.SendOnly();
                    cfg.SendFailedMessagesTo(Conventions.EndpointNamingConvention(typeof(ErrorQueueReceiver)));
                });
            }
        }

        public class ErrorQueueReceiver : EndpointConfigurationBuilder
        {
            public ErrorQueueReceiver()
            {
                var transport = Utilities.SetTransportDefaultTestsConfiguration(new AzureStorageQueueTransport(Utilities.GetEnvConfiguredConnectionString(), useNativeDelayedDeliveries: false));

                EndpointSetup(new CustomizedServer(transport), (cfg, rd) => { });
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
                        return Task.CompletedTask;
                    }

                    testContext.WasCalled = true;

                    return Task.CompletedTask;
                }
            }
        }

        public class MyMessage : IMessage
        {
            public Guid Id { get; set; }
        }
    }
}