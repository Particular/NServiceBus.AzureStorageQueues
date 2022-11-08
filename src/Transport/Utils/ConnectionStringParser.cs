// -----------------------------------------------------------------------------------------
// <copyright file="CloudStorageAccount.cs" company="Microsoft">
//    Copyright 2013 Microsoft Corporation
// 
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//      http://www.apache.org/licenses/LICENSE-2.0
// 
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.
// </copyright>
// -----------------------------------------------------------------------------------------

namespace NServiceBus.Transport.AzureStorageQueues.Utils
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using AccountSetting = System.Collections.Generic.KeyValuePair<string, System.Func<string, bool>>;
    using ConnectionStringFilter = System.Func<System.Collections.Generic.IDictionary<string, string>, System.Collections.Generic.IDictionary<string, string>>;

    /// <summary>
    /// Adapted from https://github.com/Azure/azure-storage-net/blob/master/Lib/Common/CloudStorageAccount.cs
    /// </summary>
    static class ConnectionStringParser
    {
        public const string StorageUriMustMatch = "Primary and secondary location URIs in a StorageUri must point to the same resource.";
        public const string RelativeAddressNotPermitted = "Address '{0}' is a relative address. Only absolute addresses are permitted.";

        /// <summary>
        /// The setting name for using the development storage.
        /// </summary>
        internal const string UseDevelopmentStorageSettingString = "UseDevelopmentStorage";

        /// <summary>
        /// The setting name for specifying a development storage proxy Uri.
        /// </summary>
        internal const string DevelopmentStorageProxyUriSettingString = "DevelopmentStorageProxyUri";

        /// <summary>
        /// The setting name for using the default storage endpoints with the specified protocol.
        /// </summary>
        internal const string DefaultEndpointsProtocolSettingString = "DefaultEndpointsProtocol";

        /// <summary>
        /// The setting name for the account name.
        /// </summary>
        internal const string AccountNameSettingString = "AccountName";

        /// <summary>
        /// The setting name for the account key name.
        /// </summary>
        internal const string AccountKeyNameSettingString = "AccountKeyName";

        /// <summary>
        /// The setting name for the account key.
        /// </summary>
        internal const string AccountKeySettingString = "AccountKey";

        /// <summary>
        /// The setting name for a custom blob storage endpoint.
        /// </summary>
        internal const string BlobEndpointSettingString = "BlobEndpoint";

        /// <summary>
        /// The setting name for a custom queue endpoint.
        /// </summary>
        internal const string QueueEndpointSettingString = "QueueEndpoint";

        /// <summary>
        /// The setting name for a custom table storage endpoint.
        /// </summary>
        internal const string TableEndpointSettingString = "TableEndpoint";

        /// <summary>
        /// The setting name for a custom file storage endpoint.
        /// </summary>
        internal const string FileEndpointSettingString = "FileEndpoint";

        /// <summary>
        /// The setting name for a custom blob storage secondary endpoint.
        /// </summary>
        internal const string BlobSecondaryEndpointSettingString = "BlobSecondaryEndpoint";

        /// <summary>
        /// The setting name for a custom queue secondary endpoint.
        /// </summary>
        internal const string QueueSecondaryEndpointSettingString = "QueueSecondaryEndpoint";

        /// <summary>
        /// The setting name for a custom table storage secondary endpoint.
        /// </summary>
        internal const string TableSecondaryEndpointSettingString = "TableSecondaryEndpoint";

        /// <summary>
        /// The setting name for a custom file storage secondary endpoint.
        /// </summary>
        internal const string FileSecondaryEndpointSettingString = "FileSecondaryEndpoint";

        /// <summary>
        /// The setting name for a custom storage endpoint suffix.
        /// </summary>
        internal const string EndpointSuffixSettingString = "EndpointSuffix";

        /// <summary>
        /// The setting name for a shared access key.
        /// </summary>
        internal const string SharedAccessSignatureSettingString = "SharedAccessSignature";

        /// <summary>
        /// The suffix appended to account in order to access secondary location for read only access.
        /// </summary>
        internal const string SecondaryLocationAccountSuffix = "-secondary";

        /// <summary>
        /// The default storage service hostname suffix.
        /// </summary>
        const string DefaultEndpointSuffix = "core.windows.net";

        /// <summary>
        /// The default blob storage DNS hostname prefix.
        /// </summary>
        const string DefaultBlobHostnamePrefix = "blob";

        /// <summary>
        /// The root queue DNS name prefix.
        /// </summary>
        const string DefaultQueueHostnamePrefix = "queue";

        /// <summary>
        /// The root table storage DNS name prefix.
        /// </summary>
        const string DefaultTableHostnamePrefix = "table";

        /// <summary>
        /// The default file storage DNS hostname prefix.
        /// </summary>
        const string DefaultFileHostnamePrefix = "file";

        /// <summary>
        /// Validator for the UseDevelopmentStorage setting. Must be "true".
        /// </summary>
        static readonly AccountSetting UseDevelopmentStorageSetting = Setting(UseDevelopmentStorageSettingString, "true");

        /// <summary>
        /// Validator for the DevelopmentStorageProxyUri setting. Must be a valid Uri.
        /// </summary>
        static readonly AccountSetting DevelopmentStorageProxyUriSetting = Setting(DevelopmentStorageProxyUriSettingString, IsValidUri);

        /// <summary>
        /// Validator for the DefaultEndpointsProtocol setting. Must be either "http" or "https".
        /// </summary>
        static readonly AccountSetting DefaultEndpointsProtocolSetting = Setting(DefaultEndpointsProtocolSettingString, "http", "https");

        /// <summary>
        /// Validator for the AccountName setting. No restrictions.
        /// </summary>
        static readonly AccountSetting AccountNameSetting = Setting(AccountNameSettingString);

        /// <summary>
        /// Validator for the AccountKey setting. No restrictions.
        /// </summary>
        static readonly AccountSetting AccountKeyNameSetting = Setting(AccountKeyNameSettingString);

        /// <summary>
        /// Validator for the AccountKey setting. Must be a valid base64 string.
        /// </summary>
        static readonly AccountSetting AccountKeySetting = Setting(AccountKeySettingString, IsValidBase64String);

        /// <summary>
        /// Validator for the BlobEndpoint setting. Must be a valid Uri.
        /// </summary>
        static readonly AccountSetting BlobEndpointSetting = Setting(BlobEndpointSettingString, IsValidUri);

        /// <summary>
        /// Validator for the QueueEndpoint setting. Must be a valid Uri.
        /// </summary>
        static readonly AccountSetting QueueEndpointSetting = Setting(QueueEndpointSettingString, IsValidUri);

        /// <summary>
        /// Validator for the TableEndpoint setting. Must be a valid Uri.
        /// </summary>
        static readonly AccountSetting TableEndpointSetting = Setting(TableEndpointSettingString, IsValidUri);

        /// <summary>
        /// Validator for the FileEndpoint setting. Must be a valid Uri.
        /// </summary>
        static readonly AccountSetting FileEndpointSetting = Setting(FileEndpointSettingString, IsValidUri);

        /// <summary>
        /// Validator for the BlobSecondaryEndpoint setting. Must be a valid Uri.
        /// </summary>
        static readonly AccountSetting BlobSecondaryEndpointSetting = Setting(BlobSecondaryEndpointSettingString, IsValidUri);

        /// <summary>
        /// Validator for the QueueSecondaryEndpoint setting. Must be a valid Uri.
        /// </summary>
        static readonly AccountSetting QueueSecondaryEndpointSetting = Setting(QueueSecondaryEndpointSettingString, IsValidUri);

        /// <summary>
        /// Validator for the TableSecondaryEndpoint setting. Must be a valid Uri.
        /// </summary>
        static readonly AccountSetting TableSecondaryEndpointSetting = Setting(TableSecondaryEndpointSettingString, IsValidUri);

        /// <summary>
        /// Validator for the FileSecondaryEndpoint setting. Must be a valid Uri.
        /// </summary>
        static readonly AccountSetting FileSecondaryEndpointSetting = Setting(FileSecondaryEndpointSettingString, IsValidUri);

        /// <summary>
        /// Validator for the EndpointSuffix setting. Must be a valid Uri.
        /// </summary>
        static readonly AccountSetting EndpointSuffixSetting = Setting(EndpointSuffixSettingString, IsValidDomain);

        /// <summary>
        /// Validator for the SharedAccessSignature setting. No restrictions.
        /// </summary>
        static readonly AccountSetting SharedAccessSignatureSetting = Setting(SharedAccessSignatureSettingString);

        public static bool IsValidAzureConnectionString(this string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
            {
                return false;
            }

            try
            {
                return ParseImpl(connectionString, err => { });
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Internal implementation of Parse/TryParse.
        /// </summary>
        /// <param name="connectionString">The string to parse.</param>
        /// <param name="error">A callback for reporting errors.</param>
        /// <returns>If true, the parse was successful. Otherwise, false.</returns>
        internal static bool ParseImpl(string connectionString, Action<string> error)
        {
            IDictionary<string, string> settings = ParseStringIntoSettings(connectionString, error);

            // malformed settings string
            if (settings == null)
            {
                return false;
            }

            // helper method 
            string settingOrDefault(string key)
            {
                _ = settings.TryGetValue(key, out string result);
                return result;
            }

            // devstore case
            if (MatchesSpecification(settings, AllRequired(UseDevelopmentStorageSetting), Optional(DevelopmentStorageProxyUriSetting)))
            {
                return true;
            }

            // non-devstore case
            ConnectionStringFilter endpointsOptional =
                Optional(
                    BlobEndpointSetting, BlobSecondaryEndpointSetting,
                    QueueEndpointSetting, QueueSecondaryEndpointSetting,
                    TableEndpointSetting, TableSecondaryEndpointSetting,
                    FileEndpointSetting, FileSecondaryEndpointSetting
                    );

            ConnectionStringFilter primaryEndpointRequired =
                AtLeastOne(
                    BlobEndpointSetting,
                    QueueEndpointSetting,
                    TableEndpointSetting,
                    FileEndpointSetting
                    );

            ConnectionStringFilter secondaryEndpointsOptional =
                Optional(
                    BlobSecondaryEndpointSetting,
                    QueueSecondaryEndpointSetting,
                    TableSecondaryEndpointSetting,
                    FileSecondaryEndpointSetting
                    );

            ConnectionStringFilter automaticEndpointsMatchSpec =
                MatchesExactly(MatchesAll(
                    MatchesOne(
                        MatchesAll(AllRequired(AccountKeySetting), Optional(AccountKeyNameSetting)), // Key + Name, Endpoints optional
                        AllRequired(SharedAccessSignatureSetting) // SAS + Name, Endpoints optional
                    ),
                    AllRequired(AccountNameSetting), // Name required to automatically create URIs
                    endpointsOptional,
                    Optional(DefaultEndpointsProtocolSetting, EndpointSuffixSetting)
                    ));

            ConnectionStringFilter explicitEndpointsMatchSpec =
                MatchesExactly(MatchesAll( // Any Credentials, Endpoints must be explicitly declared
                    ValidCredentials,
                    primaryEndpointRequired,
                    secondaryEndpointsOptional
                    ));

            bool matchesAutomaticEndpointsSpec = MatchesSpecification(settings, automaticEndpointsMatchSpec);
            bool matchesExplicitEndpointsSpec = MatchesSpecification(settings, explicitEndpointsMatchSpec);

            if (matchesAutomaticEndpointsSpec || matchesExplicitEndpointsSpec)
            {
                if (matchesAutomaticEndpointsSpec && !settings.ContainsKey(DefaultEndpointsProtocolSettingString))
                {
                    settings.Add(DefaultEndpointsProtocolSettingString, "https");
                }

                string blobEndpoint = settingOrDefault(BlobEndpointSettingString);
                string queueEndpoint = settingOrDefault(QueueEndpointSettingString);
                string tableEndpoint = settingOrDefault(TableEndpointSettingString);
                string fileEndpoint = settingOrDefault(FileEndpointSettingString);
                string blobSecondaryEndpoint = settingOrDefault(BlobSecondaryEndpointSettingString);
                string queueSecondaryEndpoint = settingOrDefault(QueueSecondaryEndpointSettingString);
                string tableSecondaryEndpoint = settingOrDefault(TableSecondaryEndpointSettingString);
                string fileSecondaryEndpoint = settingOrDefault(FileSecondaryEndpointSettingString);

                // if secondary is specified, primary must also be specified
                bool isValidEndpointPair(string primary, string secondary) =>
                        !string.IsNullOrWhiteSpace(primary)
                        || /* primary is null, and... */ string.IsNullOrWhiteSpace(secondary);

                StorageUri createStorageUri(string primary, string secondary, Func<IDictionary<string, string>, StorageUri> factory) =>
                        !string.IsNullOrWhiteSpace(secondary) && !string.IsNullOrWhiteSpace(primary)
                            ? new StorageUri(new Uri(primary), new Uri(secondary))
                            : !string.IsNullOrWhiteSpace(primary)
                                ? new StorageUri(new Uri(primary))
                                : matchesAutomaticEndpointsSpec && factory != null
                                    ? factory(settings)
                                    : new StorageUri(null);

                if (isValidEndpointPair(blobEndpoint, blobSecondaryEndpoint) &&
                    isValidEndpointPair(queueEndpoint, queueSecondaryEndpoint) &&
                    isValidEndpointPair(tableEndpoint, tableSecondaryEndpoint) &&
                    isValidEndpointPair(fileEndpoint, fileSecondaryEndpoint) &&
                    CanGetCredentials(settings))
                {
                    createStorageUri(blobEndpoint, blobSecondaryEndpoint, ConstructBlobEndpoint);
                    createStorageUri(queueEndpoint, queueSecondaryEndpoint, ConstructQueueEndpoint);
                    createStorageUri(tableEndpoint, tableSecondaryEndpoint, ConstructTableEndpoint);
                    createStorageUri(fileEndpoint, fileSecondaryEndpoint, ConstructFileEndpoint);

                    return true;
                }
            }

            // not valid
            error("No valid combination of account information found.");

            return false;
        }

        /// <summary>
        /// Tokenizes input and stores name value pairs.
        /// </summary>
        /// <param name="connectionString">The string to parse.</param>
        /// <param name="error">Error reporting delegate.</param>
        /// <returns>Tokenized collection.</returns>
        static IDictionary<string, string> ParseStringIntoSettings(string connectionString, Action<string> error)
        {
            IDictionary<string, string> settings = new Dictionary<string, string>();
            string[] splitted = connectionString.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string nameValue in splitted)
            {
                string[] splittedNameValue = nameValue.Split(new char[] { '=' }, 2);

                if (splittedNameValue.Length != 2)
                {
                    error("Settings must be of the form \"name=value\".");
                    return null;
                }

                if (settings.ContainsKey(splittedNameValue[0]))
                {
                    error(string.Format(CultureInfo.InvariantCulture, "Duplicate setting '{0}' found.", splittedNameValue[0]));
                    return null;
                }

                settings.Add(splittedNameValue[0], splittedNameValue[1]);
            }

            return settings;
        }

        /// <summary>
        /// Encapsulates a validation rule for an enumeration based account setting.
        /// </summary>
        /// <param name="name">The name of the setting.</param>
        /// <param name="validValues">A list of valid values for the setting.</param>
        /// <returns>An <see cref="AccountSetting"/> representing the enumeration constraint.</returns>
        static AccountSetting Setting(string name, params string[] validValues) => new(
            name,
            (settingValue) =>
            {
                if (validValues.Length == 0)
                {
                    return true;
                }

                return validValues.Contains(settingValue);
            });

        /// <summary>
        /// Encapsulates a validation rule using a func.
        /// </summary>
        /// <param name="name">The name of the setting.</param>
        /// <param name="isValid">A func that determines if the value is valid.</param>
        /// <returns>An <see cref="AccountSetting"/> representing the constraint.</returns>
        static AccountSetting Setting(string name, Func<string, bool> isValid) => new(name, isValid);

        /// <summary>
        /// Determines whether the specified setting value is a valid base64 string.
        /// </summary>
        /// <param name="settingValue">The setting value.</param>
        /// <returns><c>true</c> if the specified setting value is a valid base64 string; otherwise, <c>false</c>.</returns>
        static bool IsValidBase64String(string settingValue)
        {
            try
            {
                Convert.FromBase64String(settingValue);

                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        /// <summary>
        /// Validation function that validates Uris.
        /// </summary>
        /// <param name="settingValue">Value to validate.</param>
        /// <returns><c>true</c> if the specified setting value is a valid Uri; otherwise, <c>false</c>.</returns>
        static bool IsValidUri(string settingValue) => Uri.IsWellFormedUriString(settingValue, UriKind.Absolute);

        /// <summary>
        /// Validation function that validates a domain name.
        /// </summary>
        /// <param name="settingValue">Value to validate.</param>
        /// <returns><c>true</c> if the specified setting value is a valid domain; otherwise, <c>false</c>.</returns>
        static bool IsValidDomain(string settingValue) => Uri.CheckHostName(settingValue).Equals(UriHostNameType.Dns);

        /// <summary>
        /// Settings filter that requires all specified settings be present and valid.
        /// </summary>
        /// <param name="requiredSettings">A list of settings that must be present.</param>
        /// <returns>The remaining settings or <c>null</c> if the filter's requirement is not satisfied.</returns>
        static ConnectionStringFilter AllRequired(params AccountSetting[] requiredSettings) =>
            (settings) =>
            {
                IDictionary<string, string> result = new Dictionary<string, string>(settings);

                foreach (AccountSetting requirement in requiredSettings)
                {
                    if (result.TryGetValue(requirement.Key, out string value) && requirement.Value(value))
                    {
                        result.Remove(requirement.Key);
                    }
                    else
                    {
                        return null;
                    }
                }

                return result;
            };

        /// <summary>
        /// Settings filter that removes optional values.
        /// </summary>
        /// <param name="optionalSettings">A list of settings that are optional.</param>
        /// <returns>The remaining settings or <c>null</c> if the filter's requirement is not satisfied.</returns>
        static ConnectionStringFilter Optional(params AccountSetting[] optionalSettings) =>
            (settings) =>
            {
                IDictionary<string, string> result = new Dictionary<string, string>(settings);

                foreach (AccountSetting requirement in optionalSettings)
                {
                    if (result.TryGetValue(requirement.Key, out string value) && requirement.Value(value))
                    {
                        result.Remove(requirement.Key);
                    }
                }

                return result;
            };

        /// <summary>
        /// Settings filter that ensures that at least one setting is present.
        /// </summary>
        /// <param name="atLeastOneSettings">A list of settings of which one must be present.</param>
        /// <returns>The remaining settings or <c>null</c> if the filter's requirement is not satisfied.</returns>
        static ConnectionStringFilter AtLeastOne(params AccountSetting[] atLeastOneSettings) =>
            (settings) =>
            {
                IDictionary<string, string> result = new Dictionary<string, string>(settings);
                bool foundOne = false;

                foreach (AccountSetting requirement in atLeastOneSettings)
                {
                    if (result.TryGetValue(requirement.Key, out string value) && requirement.Value(value))
                    {
                        result.Remove(requirement.Key);
                        foundOne = true;
                    }
                }

                return foundOne ? result : null;
            };

        /// <summary>
        /// Settings filter that ensures that none of the specified settings are present.
        /// </summary>
        /// <param name="atLeastOneSettings">A list of settings of which one must not be present.</param>
        /// <returns>The remaining settings or <c>null</c> if the filter's requirement is not satisfied.</returns>
        static ConnectionStringFilter None(params AccountSetting[] atLeastOneSettings) =>
            (settings) =>
            {
                IDictionary<string, string> result = new Dictionary<string, string>(settings);

                bool foundOne = false;

                foreach (AccountSetting requirement in atLeastOneSettings)
                {
                    if (result.TryGetValue(requirement.Key, out string value) && requirement.Value(value))
                    {
                        foundOne = true;
                    }
                }

                return foundOne ? null : result;
            };

        /// <summary>
        /// Settings filter that ensures that all of the specified filters match.
        /// </summary>
        /// <param name="filters">A list of filters of which all must match.</param>
        /// <returns>The remaining settings or <c>null</c> if the filter's requirement is not satisfied.</returns>
        static ConnectionStringFilter MatchesAll(params ConnectionStringFilter[] filters) =>
            (settings) =>
            {
                IDictionary<string, string> result = new Dictionary<string, string>(settings);

                foreach (ConnectionStringFilter filter in filters)
                {
                    if (result == null)
                    {
                        break;
                    }

                    result = filter(result);
                }

                return result;
            };

        /// <summary>
        /// Settings filter that ensures that exactly one filter matches.
        /// </summary>
        /// <param name="filters">A list of filters of which exactly one must match.</param>
        /// <returns>The remaining settings or <c>null</c> if the filter's requirement is not satisfied.</returns>
        static ConnectionStringFilter MatchesOne(params ConnectionStringFilter[] filters) =>
            (settings) =>
            {
                IDictionary<string, string>[] results = filters
                    .Select(filter => filter(new Dictionary<string, string>(settings)))
                    .Where(result => result != null)
                    .Take(2)
                    .ToArray();

                if (results.Length != 1)
                {
                    return null;
                }
                else
                {
                    return results.First();
                }
            };

        /// <summary>
        /// Settings filter that ensures that the specified filter is an exact match.
        /// </summary>
        /// <param name="filter">A list of filters of which ensures that the specified filter is an exact match.</param>
        /// <returns>The remaining settings or <c>null</c> if the filter's requirement is not satisfied.</returns>
        static ConnectionStringFilter MatchesExactly(ConnectionStringFilter filter) =>
            (settings) =>
            {
                IDictionary<string, string> results = filter(settings);

                if (results == null || results.Any())
                {
                    return null;
                }
                else
                {
                    return results;
                }
            };

        /// <summary>
        /// Settings filter that ensures that a valid combination of credentials is present.
        /// </summary>
        /// <returns>The remaining settings or <c>null</c> if the filter's requirement is not satisfied.</returns>
        static readonly ConnectionStringFilter ValidCredentials =
            MatchesOne(
                MatchesAll(AllRequired(AccountNameSetting, AccountKeySetting), Optional(AccountKeyNameSetting), None(SharedAccessSignatureSetting)),    // AccountAndKey
                MatchesAll(AllRequired(SharedAccessSignatureSetting), Optional(AccountNameSetting), None(AccountKeySetting, AccountKeyNameSetting)),    // SharedAccessSignature (AccountName optional)
                None(AccountNameSetting, AccountKeySetting, AccountKeyNameSetting, SharedAccessSignatureSetting)                                        // Anonymous
            );

        /// <summary>
        /// Tests to see if a given list of settings matches a set of filters exactly.
        /// </summary>
        /// <param name="settings">The settings to check.</param>
        /// <param name="constraints">A list of filters to check.</param>
        /// <returns>
        /// If any filter returns null, false.
        /// If there are any settings left over after all filters are processed, false.
        /// Otherwise true.
        /// </returns>
        static bool MatchesSpecification(
            IDictionary<string, string> settings,
            params ConnectionStringFilter[] constraints)
        {
            foreach (ConnectionStringFilter constraint in constraints)
            {
                IDictionary<string, string> remainingSettings = constraint(settings);

                if (remainingSettings == null)
                {
                    return false;
                }
                else
                {
                    settings = remainingSettings;
                }
            }

            if (settings.Count == 0)
            {
                return true;
            }

            return false;
        }

        static bool CanGetCredentials(IDictionary<string, string> settings)
        {
            settings.TryGetValue(AccountNameSettingString, out string accountName);
            settings.TryGetValue(AccountKeySettingString, out string accountKey);
            settings.TryGetValue(AccountKeyNameSettingString, out string accountKeyName);
            settings.TryGetValue(SharedAccessSignatureSettingString, out string sharedAccessSignature);

            if (accountName != null && accountKey != null && sharedAccessSignature == null)
            {
                return true;
            }

            if (accountKey == null && accountKeyName == null && sharedAccessSignature != null)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Gets the default blob endpoint using specified settings.
        /// </summary>
        /// <param name="settings">The settings to use.</param>
        /// <returns>The default blob endpoint.</returns>
        static StorageUri ConstructBlobEndpoint(IDictionary<string, string> settings) =>
            ConstructBlobEndpoint(
                settings[DefaultEndpointsProtocolSettingString],
                settings[AccountNameSettingString],
                settings.ContainsKey(EndpointSuffixSettingString) ? settings[EndpointSuffixSettingString] : null);

        /// <summary>
        /// Gets the default blob endpoint using the specified protocol and account name.
        /// </summary>
        /// <param name="scheme">The protocol to use.</param>
        /// <param name="accountName">The name of the storage account.</param>
        /// <param name="endpointSuffix">The Endpoint DNS suffix; use <c>null</c> for default.</param>
        /// <returns>The default blob endpoint.</returns>
        static StorageUri ConstructBlobEndpoint(string scheme, string accountName, string endpointSuffix)
        {
            if (string.IsNullOrEmpty(scheme))
            {
                throw new ArgumentNullException(nameof(scheme));
            }

            if (string.IsNullOrEmpty(accountName))
            {
                throw new ArgumentNullException(nameof(accountName));
            }

            if (string.IsNullOrEmpty(endpointSuffix))
            {
                endpointSuffix = DefaultEndpointSuffix;
            }

            string primaryUri = string.Format(
                CultureInfo.InvariantCulture,
                "{0}://{1}.{2}.{3}/",
                scheme,
                accountName,
                DefaultBlobHostnamePrefix,
                endpointSuffix);

            string secondaryUri = string.Format(
                CultureInfo.InvariantCulture,
                "{0}://{1}{2}.{3}.{4}",
                scheme,
                accountName,
                SecondaryLocationAccountSuffix,
                DefaultBlobHostnamePrefix,
                endpointSuffix);

            return new StorageUri(new Uri(primaryUri), new Uri(secondaryUri));
        }

        /// <summary>
        /// Gets the default file endpoint using specified settings.
        /// </summary>
        /// <param name="settings">The settings to use.</param>
        /// <returns>The default file endpoint.</returns>
        static StorageUri ConstructFileEndpoint(IDictionary<string, string> settings) =>
            ConstructFileEndpoint(
                settings[DefaultEndpointsProtocolSettingString],
                settings[AccountNameSettingString],
                settings.ContainsKey(EndpointSuffixSettingString) ? settings[EndpointSuffixSettingString] : null);

        /// <summary>
        /// Gets the default file endpoint using the specified protocol and account name.
        /// </summary>
        /// <param name="scheme">The protocol to use.</param>
        /// <param name="accountName">The name of the storage account.</param>
        /// <param name="endpointSuffix">The Endpoint DNS suffix; use <c>null</c> for default.</param>
        /// <returns>The default file endpoint.</returns>
        static StorageUri ConstructFileEndpoint(string scheme, string accountName, string endpointSuffix)
        {
            if (string.IsNullOrEmpty(scheme))
            {
                throw new ArgumentNullException("scheme");
            }

            if (string.IsNullOrEmpty(accountName))
            {
                throw new ArgumentNullException("accountName");
            }

            if (string.IsNullOrEmpty(endpointSuffix))
            {
                endpointSuffix = DefaultEndpointSuffix;
            }

            string primaryUri = string.Format(
                CultureInfo.InvariantCulture,
                "{0}://{1}.{2}.{3}/",
                scheme,
                accountName,
                DefaultFileHostnamePrefix,
                endpointSuffix);

            string secondaryUri = string.Format(
                CultureInfo.InvariantCulture,
                "{0}://{1}{2}.{3}.{4}",
                scheme,
                accountName,
                SecondaryLocationAccountSuffix,
                DefaultFileHostnamePrefix,
                endpointSuffix);

            return new StorageUri(new Uri(primaryUri), new Uri(secondaryUri));
        }

        /// <summary>
        /// Gets the default queue endpoint using the specified settings.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <returns>The default queue endpoint.</returns>
        static StorageUri ConstructQueueEndpoint(IDictionary<string, string> settings) =>
            ConstructQueueEndpoint(
                settings[DefaultEndpointsProtocolSettingString],
                settings[AccountNameSettingString],
                settings.ContainsKey(EndpointSuffixSettingString) ? settings[EndpointSuffixSettingString] : null);

        /// <summary>
        /// Gets the default queue endpoint using the specified protocol and account name.
        /// </summary>
        /// <param name="scheme">The protocol to use.</param>
        /// <param name="accountName">The name of the storage account.</param>
        /// <param name="endpointSuffix">The Endpoint DNS suffix; use <c>null</c> for default.</param>
        /// <returns>The default queue endpoint.</returns>
        static StorageUri ConstructQueueEndpoint(string scheme, string accountName, string endpointSuffix)
        {
            if (string.IsNullOrEmpty(scheme))
            {
                throw new ArgumentNullException("scheme");
            }

            if (string.IsNullOrEmpty(accountName))
            {
                throw new ArgumentNullException("accountName");
            }

            if (string.IsNullOrEmpty(endpointSuffix))
            {
                endpointSuffix = DefaultEndpointSuffix;
            }

            string primaryUri = string.Format(
                CultureInfo.InvariantCulture,
                "{0}://{1}.{2}.{3}/",
                scheme,
                accountName,
                DefaultQueueHostnamePrefix,
                endpointSuffix);

            string secondaryUri = string.Format(
                CultureInfo.InvariantCulture,
                "{0}://{1}{2}.{3}.{4}",
                scheme,
                accountName,
                SecondaryLocationAccountSuffix,
                DefaultQueueHostnamePrefix,
                endpointSuffix);

            return new StorageUri(new Uri(primaryUri), new Uri(secondaryUri));
        }

        /// <summary>
        /// Gets the default table endpoint using the specified settings.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <returns>The default table endpoint.</returns>
        static StorageUri ConstructTableEndpoint(IDictionary<string, string> settings) =>
            ConstructTableEndpoint(
                settings[DefaultEndpointsProtocolSettingString],
                settings[AccountNameSettingString],
                settings.ContainsKey(EndpointSuffixSettingString) ? settings[EndpointSuffixSettingString] : null);

        /// <summary>
        /// Gets the default table endpoint using the specified protocol and account name.
        /// </summary>
        /// <param name="scheme">The protocol to use.</param>
        /// <param name="accountName">The name of the storage account.</param>
        /// <param name="endpointSuffix">The Endpoint DNS suffix; use <c>null</c> for default.</param>
        /// <returns>The default table endpoint.</returns>
        static StorageUri ConstructTableEndpoint(string scheme, string accountName, string endpointSuffix)
        {
            if (string.IsNullOrEmpty(scheme))
            {
                throw new ArgumentNullException("scheme");
            }

            if (string.IsNullOrEmpty(accountName))
            {
                throw new ArgumentNullException("accountName");
            }

            if (string.IsNullOrEmpty(endpointSuffix))
            {
                endpointSuffix = DefaultEndpointSuffix;
            }

            string primaryUri = string.Format(
                CultureInfo.InvariantCulture,
                "{0}://{1}.{2}.{3}/",
                scheme,
                accountName,
                DefaultTableHostnamePrefix,
                endpointSuffix);

            string secondaryUri = string.Format(
                CultureInfo.InvariantCulture,
                "{0}://{1}{2}.{3}.{4}",
                scheme,
                accountName,
                SecondaryLocationAccountSuffix,
                DefaultTableHostnamePrefix,
                endpointSuffix);

            return new StorageUri(new Uri(primaryUri), new Uri(secondaryUri));
        }
    }
}
