namespace NServiceBus.Azure.Transports.WindowsAzureStorageQueues.AcceptanceTests.Sending
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Table;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;
    using NUnit.Framework.Compatibility;

    [Category("long running")]
    public class When_delaying_messages_natively : NServiceBusAcceptanceTest
    {
        CloudTable timeoutTable;

        [SetUp]
        public new async Task SetUp()
        {
            timeoutTable = CloudStorageAccount.Parse(Utils.GetEnvConfiguredConnectionString()).CreateCloudTableClient().GetTableReference(SenderTimeoutsTable);
            if (await timeoutTable.ExistsAsync().ConfigureAwait(false))
            {
                foreach (var dte in await timeoutTable.ExecuteQuerySegmentedAsync(new TableQuery(), null).ConfigureAwait(false))
                {
                    await timeoutTable.ExecuteAsync(TableOperation.Delete(dte)).ConfigureAwait(false);
                }
            }
        }

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

                    var timeouts = await GetTimeouts();
                    Assert.AreEqual(1, timeouts.Count);

                    await MoveBeforeNow(timeouts[0]).ConfigureAwait(false);
                }))
                .WithEndpoint<Receiver>()
                .Done(c => c.WasCalled)
                .Run();
            Assert.True(context.WasCalled, "The message handler should be called");
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

                    var timeouts = await GetTimeouts();
                    await MoveBeforeNow(timeouts[0]).ConfigureAwait(false);
                }))
                .WithEndpoint<Receiver>()
                .Done(c => c.WasCalled)
                .Run();

            Assert.True(context.WasCalled, "The message should have been moved to the error queue");
        }

        async Task MoveBeforeNow(ITableEntity dte)
        {
            var earlier = DateTimeOffset.UtcNow - TimeSpan.FromMinutes(5);

            var ctx = new OperationContext();

            var earlierTimeout = new DynamicTableEntity();
            earlierTimeout.ReadEntity(dte.WriteEntity(ctx), ctx);

            earlierTimeout.PartitionKey = earlier.ToString("yyyyMMddHH");
            earlierTimeout.RowKey = earlier.ToString("yyyyMMddHHmmss");

            await timeoutTable.ExecuteAsync(TableOperation.Delete(dte)).ConfigureAwait(false);
            await timeoutTable.ExecuteAsync(TableOperation.Insert(earlierTimeout)).ConfigureAwait(false);
        }

        async Task<List<DynamicTableEntity>> GetTimeouts()
        {
            return (await timeoutTable.ExecuteQuerySegmentedAsync(new TableQuery(), null).ConfigureAwait(false)).Results;
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
                    cfg.UseTransport<AzureStorageQueueTransport>().UseNativeTimeouts(timeoutTableName: SenderTimeoutsTable);
                }).AddMapping<MyMessage>(typeof(Receiver));
            }
        }

        public class SenderToNowhere : EndpointConfigurationBuilder
        {
            public SenderToNowhere()
            {
                EndpointSetup<DefaultServer>(cfg =>
                {
                    cfg
                        .UseTransport<AzureStorageQueueTransport>()
                        .UseNativeTimeouts(timeoutTableName: SenderTimeoutsTable);
                    cfg.SendFailedMessagesTo(AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(Receiver)));
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

        const string SenderTimeoutsTable = "NativeTimeoutsSender";
    }
}