namespace NServiceBus.Transport.AzureStorageQueues.AcceptanceTests
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using global::Azure.Core;
    using global::Azure.Core.Pipeline;
    using global::Azure.Data.Tables;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;
    using Testing;

    public class When_delaying_messages_natively : NServiceBusAcceptanceTest
    {
        TableClient delayedMessagesTableClient;

        [SetUp]
        public async Task SetUpLocal()
        {
            var tableServiceClient = new TableServiceClient(Utilities.GetEnvConfiguredConnectionString());
            delayedMessagesTableClient = tableServiceClient.GetTableClient(SenderDelayedMessagesTable);

            // There is no explicit Exists method in Azure.Data.Tables, see https://github.com/Azure/azure-sdk-for-net/issues/28392
            var table = await tableServiceClient.QueryAsync(t => t.Name.Equals(SenderDelayedMessagesTable), 1).FirstOrDefaultAsync();
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
        public async Task Should_not_query_frequently_when_no_messages()
        {
            var delay = Task.Delay(TimeSpan.FromSeconds(15));

            var context = await Scenario.Define<Context>()
                .WithEndpoint<SlowlyPeekingSender>()
                .Done(c => delay.IsCompleted)
                .Run()
                .ConfigureAwait(false);

            var requestCount = context.CapturedTableServiceRequests
                .Count(request => request.Contains(SenderDelayedMessagesTable, StringComparison.OrdinalIgnoreCase) &&
                                  request.Contains("$filter", StringComparison.OrdinalIgnoreCase));

            // the wait times for next peeks for SlowlyPeekingSender
            // peek number  |wait (seconds)| cumulative wait (seconds)
            // 1 | 0 | 0
            // 2 | 1 | 1
            // 3 | 2 | 3
            // 4 | 3 | 6
            // 5 | 4 | 10
            // 6 | 5 | 15
            // 7 | 6 | 21 <- this is the boundary
            Assert.That(requestCount, Is.LessThanOrEqualTo(7));
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
                    await session.Send(new MyMessage { Id = c.TestRunId }, sendOptions).ConfigureAwait(false);

                    var delayedMessage = await delayedMessagesTableClient.QueryAsync<TableEntity>().FirstAsync().ConfigureAwait(false);

                    await MoveBeforeNow(delayedMessage).ConfigureAwait(false);
                }))
                .WithEndpoint<Receiver>()
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

            var response = await delayedMessagesTableClient.DeleteEntityAsync(original.PartitionKey, original.RowKey, cancellationToken: cancellationToken);
            Assert.That(response.IsError, Is.False);

            response = await delayedMessagesTableClient.UpsertEntityAsync(updated, cancellationToken: cancellationToken);
            Assert.That(response.IsError, Is.False);
        }

        public class Context : ScenarioContext
        {
            public bool WasCalled { get; set; }

            public IEnumerable<string> CapturedTableServiceRequests { get; set; }
        }

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                var transport = Utilities.CreateTransportWithDefaultTestsConfiguration(Utilities.GetEnvConfiguredConnectionString());
                transport.DelayedDelivery.DelayedDeliveryTableName = SenderDelayedMessagesTable;

                EndpointSetup(new CustomizedServer(transport), (cfg, rd) =>
                {
                    var routing = cfg.ConfigureRouting();
                    routing.RouteToEndpoint(typeof(MyMessage), typeof(Receiver));
                });
            }
        }

        class SlowlyPeekingSender : EndpointConfigurationBuilder
        {
            static readonly TimeSpan PeekInterval = TimeSpan.FromSeconds(1);

            public SlowlyPeekingSender()
            {
                var policy = new CaptureSendingRequestsPolicy();
                var transport = Utilities.CreateTransportWithDefaultTestsConfiguration(Utilities.GetEnvConfiguredConnectionString(), tableServiceClientPipelinePolicy: policy);
                transport.PeekInterval = PeekInterval;
                transport.DelayedDelivery.DelayedDeliveryTableName = SenderDelayedMessagesTable;

                EndpointSetup(new CustomizedServer(transport), (cfg, rd) =>
                {
                    var context = rd.ScenarioContext as Context;
                    Assert.That(context, Is.Not.Null);

                    context.CapturedTableServiceRequests = policy.Requests;
                });
            }
        }

        public class SenderToNowhere : EndpointConfigurationBuilder
        {
            public SenderToNowhere()
            {
                var transport = Utilities.CreateTransportWithDefaultTestsConfiguration(Utilities.GetEnvConfiguredConnectionString());
                transport.DelayedDelivery.DelayedDeliveryTableName = SenderDelayedMessagesTable;

                EndpointSetup(new CustomizedServer(transport), (cfg, rd) => cfg.SendFailedMessagesTo(Conventions.EndpointNamingConvention(typeof(Receiver))));
            }
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver() => EndpointSetup<DefaultServer>();

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

        /// <summary>
        /// This policy captures the URI of any request sent using the <see cref="TableServiceClient"/>.  
        /// Tests that use the <see cref="SlowlyPeekingSender"/> can access the captured requests via the test <see cref="Context"/>.
        /// </summary>
        class CaptureSendingRequestsPolicy : HttpPipelinePolicy
        {
            public ConcurrentQueue<string> Requests { get; } = new();

            public override void Process(HttpMessage message, ReadOnlyMemory<HttpPipelinePolicy> pipeline)
            {
                CaptureRequest(message);
                ProcessNext(message, pipeline);
            }

            public override async ValueTask ProcessAsync(HttpMessage message, ReadOnlyMemory<HttpPipelinePolicy> pipeline)
            {
                CaptureRequest(message);
                await ProcessNextAsync(message, pipeline);
            }

            void CaptureRequest(HttpMessage message) => Requests.Enqueue(message.Request.Uri.ToString());
        }

        const string SenderDelayedMessagesTable = "NativeDelayedMessagesForSender";
    }
}