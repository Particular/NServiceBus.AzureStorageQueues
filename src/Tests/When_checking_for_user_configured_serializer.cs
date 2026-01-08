namespace NServiceBus.Transport.AzureStorageQueues.Tests
{
    using System;
    using System.Globalization;
    using System.Reflection;
    using NUnit.Framework;
    using Serialization;
    using Settings;
    using Unicast.Messages;

    [TestFixture]
    public class When_checking_for_user_configured_serializer
    {
        [Test]
        public void Should_not_throw_exception_when_serializer_was_set()
        {
            var settings = new SettingsHolder();
            var conventions = settings.GetOrCreate<Conventions>();
            var registry = new MessageMetadataRegistry();
            registry.Initialize(conventions.IsMessageType, true);

            settings.Set(registry);

            settings.Set(AzureStorageQueueTransport.SerializerSettingsKey, Tuple.Create<SerializationDefinition, SettingsHolder>(new XmlSerializer(), settings));

            Assert.DoesNotThrow(() =>
            {
                var messageMapper = MessageWrapperSerializer.GetMapper();

                AzureStorageQueueTransport.GetMainSerializerHack(messageMapper, settings);
            });
        }
    }
}
