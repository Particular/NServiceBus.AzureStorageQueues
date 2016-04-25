namespace NServiceBus.AcceptanceTests
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using EndpointTemplates;
    using Features;
    using NUnit.Framework;
    using Serialization;

    public class When_not_configuring_serializer : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_use_JSON_serializer()
        {
            var context = await Scenario.Define<Context>()
                .WithEndpoint<DefaultConfigurationEndpoint>()
                .Done(c => c.EndpointsStarted)
                .Run();

            Assert.IsInstanceOf<JsonSerializer>(context.Serializer);
        }

        class Context : ScenarioContext
        {
            public SerializationDefinition Serializer { get; set; }
        }

        class DefaultConfigurationEndpoint : EndpointConfigurationBuilder
        {
            public DefaultConfigurationEndpoint()
            {
                EndpointSetup<DefaultServer>(c => c.EnableFeature<SerializationConfigFeature>());
            }

            class SerializationConfigFeature : Feature
            {
                protected override void Setup(FeatureConfigurationContext context)
                {
                    var testContext = context.Settings.Get<ScenarioContext>() as Context;
                    testContext.Serializer = context.Settings.Get<SerializationDefinition>();
                }
            }
        }
    }
}