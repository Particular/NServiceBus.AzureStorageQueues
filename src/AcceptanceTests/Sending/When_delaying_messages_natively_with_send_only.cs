namespace NServiceBus.Transport.AzureStorageQueues.AcceptanceTests
{
    using System;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using global::Azure.Data.Tables;
    using NServiceBus.AcceptanceTests;
    using NUnit.Framework;
    using Testing;

    public class When_delaying_messages_natively_with_send_only : NServiceBusAcceptanceTest
    {
        [SetUp]
        public async Task SetUpLocal()
        {
            var tableServiceClient = new TableServiceClient(Utilities.GetEnvConfiguredConnectionString());
            delayedMessagesTableClient = tableServiceClient.GetTableClient(SenderDelayedMessagesTable);

            // There is no explicit Exists method in Azure.Data.Tables SDK, see https://github.com/Azure/azure-sdk-for-net/issues/28392
            var table = await tableServiceClient.QueryAsync(t => t.Name.Equals(SenderDelayedMessagesTable), 1).FirstOrDefaultAsync().ConfigureAwait(false);
            if (table != null)
            {
                await foreach (var dte in delayedMessagesTableClient.QueryAsync<TableEntity>())
                {
                    var result = await delayedMessagesTableClient.DeleteEntityAsync(dte.PartitionKey, dte.RowKey).ConfigureAwait(false);
                    Assert.That(result.IsError, Is.False, $"Error {result.Status}:{result.ReasonPhrase} trying to delete {dte.PartitionKey}:{dte.RowKey} from {SenderDelayedMessagesTable} table");
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

                    var delayedMessage = await delayedMessagesTableClient.QueryAsync<TableEntity>().FirstAsync().ConfigureAwait(false);

                    await MoveBeforeNow(delayedMessage).ConfigureAwait(false);
                }))
                .WithEndpoint<ErrorQueueReceiver>()
                .Done(c => c.WasCalled)
                .Run().ConfigureAwait(false);

            Assert.That(context.WasCalled, Is.True, "The message should have been moved to the error queue");
        }

        async Task MoveBeforeNow(TableEntity original, CancellationToken cancellationToken = default)
        {
            var earlier = DateTimeOffset.UtcNow - TimeSpan.FromMinutes(5);

            var updated = new TableEntity(original)
            {
                PartitionKey = earlier.ToString("yyyyMMddHH"),
                RowKey = earlier.ToString("yyyyMMddHHmmss")
            };

            var response = await delayedMessagesTableClient.DeleteEntityAsync(original.PartitionKey, original.RowKey, cancellationToken: cancellationToken).ConfigureAwait(false);
            Assert.That(response.IsError, Is.False);

            response = await delayedMessagesTableClient.UpsertEntityAsync(updated, cancellationToken: cancellationToken).ConfigureAwait(false);
            Assert.That(response.IsError, Is.False);
        }

        TableClient delayedMessagesTableClient;

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
                readonly Context testContext;

                public MyMessageHandler(Context testContext) => this.testContext = testContext;

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