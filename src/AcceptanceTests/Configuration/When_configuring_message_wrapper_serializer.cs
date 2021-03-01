namespace NServiceBus.Transport.AzureStorageQueues.AcceptanceTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.Serialization.Formatters.Binary;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Azure.Transports.WindowsAzureStorageQueues;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using MessageInterfaces;
    using Serialization;
    using NUnit.Framework;
    using Settings;

    public class When_configuring_message_wrapper_serializer : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_use_configured_serializer_for_wrapper_message()
        {
            var context = await Scenario.Define<Context>()
                .WithEndpoint<DefaultConfigurationEndpoint>(c => c
                    .When(e => e.SendLocal(new MyRequest())))
                .Done(c => c.InvokedHandler)
                .Run().ConfigureAwait(false);

            Assert.IsTrue(context.InvokedHandler);
            Assert.IsTrue(context.SerializedWrapper);
            Assert.IsTrue(context.DeserializedWrapper);
        }

        class Context : ScenarioContext
        {
            public bool SerializedWrapper { get; set; }
            public bool DeserializedWrapper { get; set; }
            public bool InvokedHandler { get; set; }
        }

        class DefaultConfigurationEndpoint : EndpointConfigurationBuilder
        {
            public DefaultConfigurationEndpoint()
            {
                EndpointSetup<DefaultServer>(e =>
                {
                    e.UseSerialization<NewtonsoftSerializer>();

                    var transport = e.ConfigureTransport<AzureStorageQueueTransport>();
                    transport.MessageWrapperSerializationDefinition = new TestIndependence.TestIdAppendingSerializationDefinition<CustomSerializer>();
                });
            }

            class MyRequestHandler : IHandleMessages<MyRequest>
            {
                public MyRequestHandler(Context scenarioContext)
                {
                    this.scenarioContext = scenarioContext;
                }

                public Task Handle(MyRequest message, IMessageHandlerContext context)
                {
                    scenarioContext.InvokedHandler = true;
                    return Task.FromResult(0);
                }

                Context scenarioContext;
            }
        }

        public class MyRequest : IMessage
        {
        }

        class CustomSerializer : SerializationDefinition
        {
            public override Func<IMessageMapper, IMessageSerializer> Configure(ReadOnlySettings settings)
            {
                return mapper => new MyCustomSerializer(settings.Get<ScenarioContext>());
            }
        }

        class MyCustomSerializer : IMessageSerializer
        {
            public MyCustomSerializer(ScenarioContext scenarioContext)
            {
                this.scenarioContext = (Context)scenarioContext;
            }

            public void Serialize(object message, Stream stream)
            {
                if (message is MessageWrapper)
                {
                    scenarioContext.SerializedWrapper = true;
                }

                var serializer = new BinaryFormatter();
                serializer.Serialize(stream, message);
            }

            public object[] Deserialize(Stream stream, IList<Type> messageTypes = null)
            {
                var serializer = new BinaryFormatter();

                stream.Position = 0;
                var message = serializer.Deserialize(stream);

                if (message is MessageWrapper)
                {
                    scenarioContext.DeserializedWrapper = true;
                }

                return new[]
                {
                    message
                };
            }

            public string ContentType => "MyCustomSerializer";
            Context scenarioContext;
        }
    }
}