namespace NServiceBus.AzureStorageQueues
{
    using System;
    using NServiceBus.Settings;

    internal static class SettingsExtensions
    {
        /// <summary>
        ///     Applies the settings value when present by invoking <paramref name="apply" />.
        /// </summary>
        public static void TryApplyValue<TValue>(this ReadOnlySettings settings, string key, Action<TValue> apply)
        {
            TValue value;
            if (settings.TryGet(key, out value))
            {
                apply(value);
            }
        }
    }
}