namespace NServiceBus.Transport.AzureStorageQueues.AcceptanceTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
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
            public override Func<IMessageMapper, IMessageSerializer> Configure(IReadOnlySettings settings)
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

                var serializer = new System.Runtime.Serialization.DataContractSerializer(typeof(MessageWrapper));
                serializer.WriteObject(stream, message);
            }

            public object[] Deserialize(ReadOnlyMemory<byte> body, IList<Type> messageTypes = null)
            {
                var serializer = new System.Runtime.Serialization.DataContractSerializer(typeof(MessageWrapper));
                object message;
                using (var stream = new MemoryStream(body.ToArray()))
                {
                    stream.Position = 0;
                    message = serializer.ReadObject(stream);
                }

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