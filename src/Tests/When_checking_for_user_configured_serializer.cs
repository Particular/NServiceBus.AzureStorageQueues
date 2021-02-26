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
    class When_checking_for_user_configured_serializer
    {
        [Test]
        public void Should_throw_exception_when_no_serializer_was_set()
        {
            var exception = Assert.Throws<Exception>(() =>
            {
                var settings = new SettingsHolder();
                var messageMapper = MessageWrapperSerializer.GetMapper();

                AzureStorageQueueTransport.GetMainSerializerHack(messageMapper, settings);
            });

            Assert.IsTrue(exception.Message.StartsWith("No serializer defined. If the transport is used in combination with NServiceBus, use 'endpointConfiguration.UseSerialization<T>();' to select a serializer."), $"Incorrect exception message: {exception.Message}");
        }

        [Test]
        public void Should_not_throw_exception_when_serializer_was_set()
        {
            var settings = new SettingsHolder();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.CreateInstance;

            var conventions = settings.GetOrCreate<Conventions>();
            var registry = (MessageMetadataRegistry)Activator.CreateInstance(typeof(MessageMetadataRegistry), flags, null, new object[] { new Func<Type, bool>(t => conventions.IsMessageType(t)) }, CultureInfo.InvariantCulture);

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
