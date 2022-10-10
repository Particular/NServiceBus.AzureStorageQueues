namespace NServiceBus.Transport.AzureStorageQueues.AcceptanceTests
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using global::Azure.Core;
    using global::Azure.Core.Pipeline;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;
    using Testing;
    using LogLevel = Logging.LogLevel;

    public class When_dispatching_fails : NServiceBusAcceptanceTest
    {
        [Test]
        public void Should_log_send_related_error() =>
            Assert.ThrowsAsync<Exception>(() => Scenario.Define<Context>()
                .WithEndpoint<FaultySender>()
                .Done(c => c.Logs.Any(li => li.Message.Contains("Fail on proxy") && li.Level == LogLevel.Error))
                .Run());

        [Test]
        public void Should_log_queue_related_error_if_queue_doesnt_exist()
        {
            const string queue = "non-existent";

            Assert.ThrowsAsync<Exception>(() => Scenario.Define<Context>()
                .WithEndpoint<Sender>(b => b.When((bus, c) =>
                {
                    var options = new SendOptions();
                    options.SetDestination(queue);
                    return bus.Send(new MyMessage(), options);
                }))
                .Done(c => c.Logs.Any(li => li.Message.Contains($"The queue {queue} was not found. Create the queue.") && li.Level == LogLevel.Error))
                .Run());
        }

        public class Context : ScenarioContext
        {
        }

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender() => EndpointSetup<DefaultServer>(cfg => { });
        }

        public class FaultySender : EndpointConfigurationBuilder
        {
            public FaultySender()
            {
                AzureStorageQueueTransport transport = Utilities.CreateTransportWithDefaultTestsConfiguration(
                    Utilities.GetEnvConfiguredConnectionString(),
                    tableServiceClientPipelinePolicy: new FailSendingRequestsPolicy());

                EndpointSetup(new CustomizedServer(transport), (cfg, rd) => { });
            }
        }

        public class MyMessage : IMessage
        {
        }

        class FailSendingRequestsPolicy : HttpPipelinePolicy
        {
            public override void Process(HttpMessage message, ReadOnlyMemory<HttpPipelinePolicy> pipeline) => throw new Exception("Fail on proxy");

            public override ValueTask ProcessAsync(HttpMessage message, ReadOnlyMemory<HttpPipelinePolicy> pipeline) => throw new Exception("Fail on proxy");
        }
    }
}