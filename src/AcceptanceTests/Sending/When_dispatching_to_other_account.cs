namespace NServiceBus.AcceptanceTests.WindowsAzureStorageQueues.Sending
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Support;
    using EndpointTemplates;
    using NUnit.Framework;
    using ScenarioDescriptors;

    public class When_dispatching_to_another_account : NServiceBusAcceptanceTest
    {
        [Test]
        public void Connection_string_should_throw()
        {
            var ex = Assert.ThrowsAsync<AggregateException>(() => RunTest(MainNamespaceConnectionString));

            Assert.IsInstanceOf<KeyNotFoundException>(ex.InnerExceptions.Cast<ScenarioException>().Single().InnerException);
        }

        [Test]
        public async Task Account_mapped_should_be_respected()
        {
            await RunTest(AnotherAccountName);
        }

        static async Task RunTest(string connectionStringOrName)
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

        const string AnotherAccountName = "another";
        const string DefaultAccountName = "default";
        static readonly string MainNamespaceConnectionString = Transports.Default.Settings.Get<string>("Transport.ConnectionString");

        public class Context : ScenarioContext
        {
            public string SendTo { get; set; }
            public bool WasCalled { get; set; }
        }

        class Endpoint : EndpointConfigurationBuilder
        {
            public Endpoint()
            {
                EndpointSetup<DefaultServer>(configuration =>
                {
                    configuration.UseTransport<AzureStorageQueueTransport>()
                        .UseAccountNamesInsteadOfConnectionStrings()
                        .DefaultAccountName(DefaultAccountName)
                        .AccountRouting()
                        .AddAccount(AnotherAccountName, Transports.Default.Settings.Get<string>("Transport.ConnectionString"));
                }).AddMapping<MyMessage>(typeof(Receiver));
            }
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServer>(configuration =>
                {
                    configuration.UseTransport<AzureStorageQueueTransport>()
                        .UseAccountNamesInsteadOfConnectionStrings()
                        .DefaultAccountName(AnotherAccountName)
                        .AccountRouting()
                        .AddAccount(DefaultAccountName, Transports.Default.Settings.Get<string>("Transport.ConnectionString"));
                });
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
    }
}