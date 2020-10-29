namespace NServiceBus.AcceptanceTests.WindowsAzureStorageQueues.Sending
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using EndpointTemplates;
    using NUnit.Framework;

    public class When_dispatching_to_another_account_using_alias : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Alias_mapped_should_be_respected()
        {
            var context = await Scenario.Define<Context>()
                .WithEndpoint<Sender>(b =>
                {
                    b.When((bus, c) =>
                    {
                        var options = new SendOptions();
                        options.SetDestination($"{Conventions.EndpointNamingConvention(typeof(Receiver))}@{Alias}");
                        return bus.Send(new MyMessage(), options);
                    });
                })
                .WithEndpoint<Receiver>()
                .Done(c => c.WasCalled)
                .Run().ConfigureAwait(false);

            Assert.IsTrue(context.WasCalled);

        }

        const string Alias = "another";
        const string DefaultAccountName = "default";

        public class Context : ScenarioContext
        {
            public string SendTo { get; set; }
            public bool WasCalled { get; set; }
        }

        class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServer>(configuration =>
                {
                    configuration.UseTransport<AzureStorageQueueTransport>()
                        .DefaultAccountAlias(DefaultAccountName)
                        .ConnectionString(ConfigureEndpointAzureStorageQueueTransport.ConnectionString)
                        .AccountRouting()
                        .AddAccount(Alias, ConfigureEndpointAzureStorageQueueTransport.AnotherConnectionString);

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
                        .DefaultAccountAlias(Alias)
                        .ConnectionString(ConfigureEndpointAzureStorageQueueTransport.AnotherConnectionString);
                });
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
                    testContext.WasCalled = true;
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