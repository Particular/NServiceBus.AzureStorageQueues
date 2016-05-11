namespace NServiceBus.AcceptanceTests.WindowsAzureStorageQueues.Configuration
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Support;
    using Azure.Transports.WindowsAzureStorageQueues.AcceptanceTests;
    using EndpointTemplates;
    using Features;
    using NUnit.Framework;
    
    public class When_configuring_account_names : NServiceBusAcceptanceTest
    {
        const string Default = "default";
        const string Another = "another";

        readonly string connectionString;
        readonly string anotherConnectionString;

        public When_configuring_account_names()
        {
            connectionString = Utils.GetEnvConfiguredConnectionString();
            anotherConnectionString = Utils.BuildAnotherConnectionString(connectionString);
        }

        [Test]
        public async void Should_accept_no_mappings_at_all()
        {
            await Configure(_ => { }).ConfigureAwait(false);
        }

        [Test]
        public void Should_not_accept_mappings_without_default()
        {
            var ex = Assert.Throws<AggregateException>(async () => { await Configure(m => { m.MapAccount(Another, anotherConnectionString); }).ConfigureAwait(false); });
            var inner = ex.InnerExceptions.OfType<ScenarioException>().Single();
            Assert.IsInstanceOf<ArgumentException>(inner.InnerException);
        }

        [Test]
        public async void Should_accept_mappings_with_default()
        {
            await Configure(m =>
            {
                m.MapLocalAccount(Default);
                m.MapAccount(Another, anotherConnectionString);
            }).ConfigureAwait(false);
        }

        Task Configure(Action<AccountMapping> action)
        {
            return Scenario.Define<Context>()
                .WithEndpoint<Endpoint>(cfg => cfg.CustomConfig(c =>
                  {
                      c.UseTransport<NServiceBus.AzureStorageQueueTransport>()
                          .UseAccountNamesInsteadOfConnectionStrings(action)
                          .ConnectionString(connectionString)
                          .SerializeMessageWrapperWith<JsonSerializer>();

                  }))
                .Done(c => c.WasStarted)
                .Run();
        }

        public class Context : ScenarioContext
        {
            public bool WasStarted { get; set; }
        }

        public class Endpoint : EndpointConfigurationBuilder
        {
            public Endpoint()
            {
                EndpointSetup<DefaultServer>(c => { c.EnableFeature<Bootstrapper>(); }).SendOnly();
            }

            public class Bootstrapper : Feature
            {
                public Bootstrapper()
                {
                    EnableByDefault();
                }

                protected override void Setup(FeatureConfigurationContext context)
                {
                    context.RegisterStartupTask(b => new MyTask(b.Build<Context>()));
                }

                public class MyTask : FeatureStartupTask
                {
                    public MyTask(Context scenarioContext)
                    {
                        this.scenarioContext = scenarioContext;
                    }

                    protected override Task OnStart(IMessageSession session)
                    {
                        scenarioContext.WasStarted = true;
                        return Task.FromResult(0);
                    }

                    protected override Task OnStop(IMessageSession session)
                    {
                        return Task.FromResult(0);
                    }

                    readonly Context scenarioContext;
                }
            }
        }
    }
}