namespace NServiceBus.AcceptanceTests.WindowsAzureStorageQueues.Configuration
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Support;
    using Azure.Transports.WindowsAzureStorageQueues.AcceptanceTests;
    using EndpointTemplates;
    using NUnit.Framework;

    public class When_configuring_account_names : NServiceBusAcceptanceTest
    {
        public When_configuring_account_names()
        {
            connectionString = Utils.GetEnvConfiguredConnectionString();
            anotherConnectionString = Utils.BuildAnotherConnectionString(connectionString);
        }

        [Test]
        public Task Should_accept_no_mappings_at_all()
        {
            return Configure(_ => { });
        }

        [Test]
        public void Should_not_accept_mappings_without_default()
        {
            var ex = Assert.ThrowsAsync<AggregateException>(() => { return Configure(m => { m.MapAccount(Another, anotherConnectionString); }); });
            var inner = ex.InnerExceptions.OfType<ScenarioException>().Single();
            Assert.IsInstanceOf<ArgumentException>(inner.InnerException);
        }

        [Test]
        public Task Should_accept_mappings_with_default()
        {
            return Configure(m =>
            {
                m.MapLocalAccount(Default);
                m.MapAccount(Another, anotherConnectionString);
            });
        }

        Task Configure(Action<AccountMapping> action)
        {
            return Scenario.Define<Context>()
                .WithEndpoint<Endpoint>(cfg => cfg.CustomConfig(c =>
                {
                    c.UseTransport<AzureStorageQueueTransport>()
                        .UseAccountNamesInsteadOfConnectionStrings(action)
                        .ConnectionString(connectionString)
                        .SerializeMessageWrapperWith<JsonSerializer>();
                }))
                .Done(c => c.WasStarted)
                .Run();
        }

        readonly string connectionString;
        readonly string anotherConnectionString;
        const string Default = "default";
        const string Another = "another";

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

            public class Bootstrapper : Features.Feature
            {
                public Bootstrapper()
                {
                    EnableByDefault();
                }

                protected override void Setup(Features.FeatureConfigurationContext context)
                {
                    context.RegisterStartupTask(b => new MyTask(b.Build<Context>()));
                }

                public class MyTask : Features.FeatureStartupTask
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