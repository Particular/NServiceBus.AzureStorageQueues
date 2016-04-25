namespace NServiceBus.AcceptanceTests
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using EndpointTemplates;
    using Features;
    using NUnit.Framework;
    using Serialization;

    public class When_configuring_serializer : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_use_configured_serializer()
        {
            var context = await Scenario.Define<Context>()
                .WithEndpoint<CustomConfigurationEndpoint>()
                .Done(c => c.EndpointsStarted)
                .Run();

            Assert.IsInstanceOf<XmlSerializer>(context.Serializer);
        }

        class Context : ScenarioContext
        {
            public SerializationDefinition Serializer { get; set; }
        }

        class CustomConfigurationEndpoint : EndpointConfigurationBuilder
        {
            public CustomConfigurationEndpoint()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.EnableFeature<SerializationConfigFeature>();
                    c.UseSerialization<XmlSerializer>();
                });
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