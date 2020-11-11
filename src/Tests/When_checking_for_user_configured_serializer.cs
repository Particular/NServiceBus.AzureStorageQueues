namespace NServiceBus.Transport.AzureStorageQueues.Tests
{
    using System;
    using NUnit.Framework;
    using Serialization;
    using Settings;

    [TestFixture]
    class When_checking_for_user_configured_serializer
    {
        [Test]
        public void Should_throw_exception_when_no_serializer_was_set()
        {
            var exception = Assert.Throws<Exception>(() => Guard.AgainstUnsetSerializerSetting(new SettingsHolder()));

            Assert.IsTrue(exception.Message.StartsWith("Use 'endpointConfiguration.UseSerialization<T>();'"), $"Incorrect exception message: {exception.Message}");
        }

        [Test]
        public void Should_not_throw_exception_when_serializer_was_set()
        {
            var settings = new SettingsHolder();

            settings.Set(AzureStorageQueueTransport.SerializerSettingsKey, Tuple.Create<SerializationDefinition,SettingsHolder>(new XmlSerializer(), settings));

            Assert.DoesNotThrow(() => Guard.AgainstUnsetSerializerSetting(settings));
        }
    }
}
