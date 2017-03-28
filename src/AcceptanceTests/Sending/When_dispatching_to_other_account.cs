namespace NServiceBus.AcceptanceTests.WindowsAzureStorageQueues.Sending
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using EndpointTemplates;
    using NUnit.Framework;

    public class When_dispatching_to_another_account : NServiceBusAcceptanceTest
    {
        [Test]
        public void Connection_string_should_throw()
        {
            Assert.ThrowsAsync<KeyNotFoundException>(() => RunTest(MainNamespaceConnectionString));
        }

        [Test]
        public Task Account_mapped_should_be_respected()
        {
            return RunTest(AnotherAccountName);
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
        static readonly string MainNamespaceConnectionString = ConfigureEndpointAzureStorageQueueTransport.ConnectionString;

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
                        .UseAccountAliasesInsteadOfConnectionStrings()
                        .DefaultAccountAlias(DefaultAccountName)
                        .AccountRouting()
                        .AddAccount(AnotherAccountName, ConfigureEndpointAzureStorageQueueTransport.ConnectionString);

                    configuration.ConfigureTransport().Routing().RouteToEndpoint(typeof(MyMessage), typeof(Receiver));
                });
            }
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServer>(configuration =>
                {
                    configuration.UseTransport<AzureStorageQueueTransport>()
                        .UseAccountAliasesInsteadOfConnectionStrings()
                        .DefaultAccountAlias(AnotherAccountName)
                        .AccountRouting()
                        .AddAccount(DefaultAccountName, ConfigureEndpointAzureStorageQueueTransport.ConnectionString);
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