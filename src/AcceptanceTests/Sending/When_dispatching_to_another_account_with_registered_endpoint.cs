namespace NServiceBus.AcceptanceTests.WindowsAzureStorageQueues.Sending
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using EndpointTemplates;
    using NUnit.Framework;

    public class When_dispatching_to_another_account_with_registered_endpoint : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Account_mapped_should_be_respected()
        {
           var context = await Scenario.Define<Context>()
                .WithEndpoint<Endpoint>(b =>
                {
                    b.When((bus, c) => bus.Send(new MyMessage()));
                })
                .WithEndpoint<Receiver>()
                .Done(c => c.WasCalled)
                .Run().ConfigureAwait(false);

            Assert.IsTrue(context.WasCalled);
        }

        const string AnotherAccountName = "another";
        const string DefaultAccountName = "default";

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
                    var routing = configuration.UseTransport<AzureStorageQueueTransport>()
                        .DefaultAccountAlias(DefaultAccountName)
                        .ConnectionString(ConfigureEndpointAzureStorageQueueTransport.ConnectionString)
                        .AccountRouting();

                    var anotherAccount = routing.AddAccount(AnotherAccountName, ConfigureEndpointAzureStorageQueueTransport.AnotherConnectionString);
                    anotherAccount.RegisteredEndpoints.Add(Conventions.EndpointNamingConvention(typeof(Receiver)));

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
                        .ConnectionString(ConfigureEndpointAzureStorageQueueTransport.AnotherConnectionString);
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