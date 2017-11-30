namespace NServiceBus.Azure.Transports.WindowsAzureStorageQueues.AcceptanceTests.Sending
{
    using System;
    using System.Diagnostics;
    using System.Reflection;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Configuration.AdvanceExtensibility;
    using Features;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Table;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_delaying_messages_natively_in_hybrid_mode : NServiceBusAcceptanceTest
    {
        CloudTable delayedMessagesTable;

        [SetUp]
        public new async Task SetUp()
        {
            delayedMessagesTable = CloudStorageAccount.Parse(Utils.GetEnvConfiguredConnectionString()).CreateCloudTableClient().GetTableReference(SenderDelayedMessagesTable);
            var tableExists = await delayedMessagesTable.ExistsAsync().ConfigureAwait(false);
            if (tableExists)
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
            var delay = TimeSpan.FromSeconds(10);

            var context = await Scenario.Define<Context>()
                .WithEndpoint<Sender>(b => b.When((session, c) =>
                {
                    var sendOptions = new SendOptions();
                    sendOptions.DelayDeliveryWith(delay);
                    c.Stopwatch = Stopwatch.StartNew();
                    sendOptions.RouteToThisEndpoint();
                    return session.Send(new MyMessage { Id = c.TestRunId }, sendOptions);
                }))
                .Done(c => c.WasCalled)
                .Run(delay + TimeSpan.FromSeconds(30)).ConfigureAwait(false);

            Assert.True(context.WasCalled, "The message handler should be called");
            Assert.Greater(context.Stopwatch.Elapsed, delay);
        }

       

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
                    var transport = cfg.UseTransport<AzureStorageQueueTransport>();

                    var delayedDeliverySettings = transport.DelayedDelivery();
                    delayedDeliverySettings.UseTableName(SenderDelayedMessagesTable);
                    
                    // run in hybrid mode, i.e. timeout manager and native delayed delivery need to be enabled
                    // delayedDeliverySettings.DisableTimeoutManager(); is invoked by IConfigureEndpointTestExecution - need to undo it to allow hybrid mode
                    var fieldInfo = typeof(DelayedDeliverySettings).GetField("TimeoutManagerDisabled", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    fieldInfo.SetValue(delayedDeliverySettings, false);
                    // re-enable timeout manager back again
                    transport.GetSettings().Set(typeof(TimeoutManager).FullName, FeatureState.Enabled);
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

        const string SenderDelayedMessagesTable = "NativeDelayedMessagesForSender";
    }
}