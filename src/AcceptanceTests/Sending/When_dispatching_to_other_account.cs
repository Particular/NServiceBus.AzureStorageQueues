﻿namespace NServiceBus.Azure.Transports.WindowsAzureStorageQueues.AcceptanceTests.Sending
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTesting.Support;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NServiceBus.AcceptanceTests.ScenarioDescriptors;
    using NUnit.Framework;

    public class When_dispatching_to_another_account : NServiceBusAcceptanceTest
    {
        private const string MainNamespaceName = "MainNamespaceName";
        public static readonly string MainNamespaceConnectionString = Transports.Default.Settings["Transport.ConnectionString"];

        [Test]
        public async Task Connection_string_should_be_respected()
        {
            await RunTest(MainNamespaceConnectionString);
        }

        [Test]
        public async Task Namespace_mapped_should_be_respected()
        {
            await RunTest(MainNamespaceName);
        }

        private static async Task RunTest(string connectionStringOrName)
        {
            var context = await Scenario.Define<Context>()
                .WithEndpoint<Endpoint>(b =>
                {
                    b.When((bus, c) =>
                    {
                        var options = new SendOptions();
                        options.SetDestination("DispatchingToAnotherAccount.Receiver@" + connectionStringOrName);
                        return bus.Send(new MyMessage(), options);
                    });
                })
                .WithEndpoint<Receiver>()
                .Done(c => c.WasCalled)
                .Run();

            Assert.IsTrue(context.WasCalled);
        }

        public class Context : ScenarioContext
        {
            public string SendTo { get; set; }
            public bool WasCalled { get; set; }
        }

        public class Endpoint : EndpointConfigurationBuilder
        {
            public Endpoint()
            {
                EndpointSetup<DefaultServer>()
                    .AddMapping<MyMessage>(typeof(Receiver));
            }
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServer>();
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public Context Context { get; set; }

                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    Context.WasCalled = true;
                    return Task.FromResult(0);
                }
            }
        }

        [Serializable]
        public class MyMessage : ICommand
        {
        }

        public class Configuration : IConfigureTestExecution
        {
            public Task Configure(BusConfiguration configuration, IDictionary<string, string> settings)
            {
                configuration.UseTransport<AzureStorageQueueTransport>()
                    .Addressing()
                    .Partitioning()
                    .AddStorageAccount(MainNamespaceName, Transports.Default.Settings["Transport.ConnectionString"]);

                return Task.FromResult(0);
            }

            public Task Cleanup()
            {
                return Task.FromResult(0);
            }
        }
    }
}