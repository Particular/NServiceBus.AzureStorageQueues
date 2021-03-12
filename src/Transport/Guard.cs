namespace NServiceBus
{
    using System;
    using NServiceBus.Serialization;
    using NServiceBus.Settings;

    static class Guard
    {
        public static void AgainstNull(string argumentName, object value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(argumentName);
            }
        }

        public static void AgainstNullAndEmpty(string argumentName, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentNullException(argumentName);
            }
        }

        public static void AgainstNegativeAndZero(string argumentName, TimeSpan value)
        {
            if (value <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(argumentName);
            }
        }

        public static void AgainstUnsetSerializerSetting(SettingsHolder settings)
        {
            if (!settings.TryGet<Tuple<SerializationDefinition, SettingsHolder>>(AzureStorageQueueTransport.SerializerSettingsKey, out var _))
            {
                throw new Exception("Use 'endpointConfiguration.UseSerialization<T>();' to select a serializer. If you are upgrading, install the `NServiceBus.Newtonsoft.Json` NuGet package and consult the upgrade guide for further information.");
            }
        }
    }
}
