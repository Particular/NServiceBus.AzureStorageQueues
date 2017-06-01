﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NServiceBus.Azure.Transports.WindowsAzureStorageQueues;
using NServiceBus.MessageInterfaces;
using NServiceBus.Pipeline;
using NServiceBus.Serialization;
using NServiceBus.Settings;

public class TestIndependence 
{
    const string HeaderName = "$AcceptanceTesting.TestRunId";

    public class TestIdAppendingJSON : SerializationDefinition
    {
        public override Func<IMessageMapper, IMessageSerializer> Configure(ReadOnlySettings settings)
        {
            var builder = new JsonSerializer().Configure(settings);
            var scenarioContext = settings.GetOrDefault<ScenarioContext>();

            return mapper => Builder(builder, mapper, scenarioContext);
        }

        static IMessageSerializer Builder(Func<IMessageMapper, IMessageSerializer> builder, IMessageMapper mapper, ScenarioContext scenarioContext)
        {
            var serializer = builder(mapper);
            return new TestIdSerializer(serializer, GetTestRunId(scenarioContext));
        }

        class TestIdSerializer : IMessageSerializer
        {
            readonly IMessageSerializer serializer;
            readonly string testId;

            public TestIdSerializer(IMessageSerializer serializer, string testId)
            {
                this.serializer = serializer;
                this.testId = testId;
            }

            public void Serialize(object message, Stream stream)
            {
                ((MessageWrapper) message).Headers[HeaderName] = testId;
                serializer.Serialize(message, stream);
            }

            public object[] Deserialize(Stream stream, IList<Type> messageTypes = null) => serializer.Deserialize(stream, messageTypes);

            public string ContentType => serializer.ContentType;
        }
    }

    public class SkipBehavior : IBehavior<ITransportReceiveContext, ITransportReceiveContext>
    {
        string testRunId;

        public SkipBehavior(ScenarioContext scenarioContext)
        {
            testRunId = GetTestRunId(scenarioContext);
        }

        public Task Invoke(ITransportReceiveContext context, Func<ITransportReceiveContext, Task> next)
        {
            string runId;
            if (!context.Message.Headers.TryGetValue(HeaderName, out runId) || runId != testRunId)
            {
                Console.WriteLine($"Skipping message {context.Message.MessageId} from previous test run");
                return Task.FromResult(0);
            }

            return next(context);
        }
    }

    static string GetTestRunId(ScenarioContext scenarioContext)
    {
        return scenarioContext.TestRunId.ToString();
    }
}