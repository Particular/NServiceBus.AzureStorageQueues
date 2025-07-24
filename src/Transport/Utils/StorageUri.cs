//-----------------------------------------------------------------------
// <copyright file="StorageUri.cs" company="Microsoft">
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
//-----------------------------------------------------------------------

namespace NServiceBus.Transport.AzureStorageQueues
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    /// <summary>
    /// Contains the URIs for both the primary and secondary locations of a Microsoft Azure Storage resource.
    /// Adapted from https://github.com/Azure/azure-storage-net/blob/b0d47bfd4c1c0dcbe60eaf373e2977cd7405aa5f/Lib/Common/StorageUri.cs
    /// </summary>
    sealed class StorageUri : IEquatable<StorageUri>
    {
        /// <summary>
        /// The endpoint for the primary location for the storage account.
        /// </summary>
        /// <value>The <see cref="Uri"/> for the primary endpoint.</value>
        public Uri PrimaryUri
        {
            get;

            private init
            {
                AssertAbsoluteUri(value);
                field = value;
            }
        }

        /// <summary>
        /// The endpoint for the secondary location for the storage account.
        /// </summary>
        /// <value>The <see cref="Uri"/> for the secondary endpoint.</value>
        public Uri SecondaryUri
        {
            get;

            private init
            {
                AssertAbsoluteUri(value);
                field = value;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="StorageUri"/> class using the primary endpoint for the storage account.
        /// </summary>
        /// <param name="primaryUri">The <see cref="Uri"/> for the primary endpoint.</param>
        public StorageUri(Uri primaryUri)
            : this(primaryUri, null /* secondaryUri */)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="StorageUri"/> class using the primary and secondary endpoints for the storage account.
        /// </summary>
        /// <param name="primaryUri">The <see cref="Uri"/> for the primary endpoint.</param>
        /// <param name="secondaryUri">The <see cref="Uri"/> for the secondary endpoint.</param>
        public StorageUri(Uri primaryUri, Uri secondaryUri)
        {
            if ((primaryUri != null) && (secondaryUri != null))
            {
                bool primaryUriPathStyle = UsePathStyleAddressing(primaryUri);
                bool secondaryUriPathStyle = UsePathStyleAddressing(secondaryUri);

                if (!primaryUriPathStyle && !secondaryUriPathStyle)
                {
                    if (primaryUri.PathAndQuery != secondaryUri.PathAndQuery)
                    {
                        throw new ArgumentException(ConnectionStringParser.StorageUriMustMatch, nameof(secondaryUri));
                    }
                }
                else
                {
                    IEnumerable<string> primaryUriSegments = primaryUri.Segments.Skip(primaryUriPathStyle ? 2 : 0);
                    IEnumerable<string> secondaryUriSegments = secondaryUri.Segments.Skip(secondaryUriPathStyle ? 2 : 0);

                    if (!primaryUriSegments.SequenceEqual(secondaryUriSegments) || (primaryUri.Query != secondaryUri.Query))
                    {
                        throw new ArgumentException(ConnectionStringParser.StorageUriMustMatch, nameof(secondaryUri));
                    }
                }
            }

            PrimaryUri = primaryUri;
            SecondaryUri = secondaryUri;
        }

        /// <summary>
        /// List of ports used for path style addressing.
        /// </summary>
        static readonly int[] PathStylePorts = { 10000, 10001, 10002, 10003, 10004, 10100, 10101, 10102, 10103, 10104, 11000, 11001, 11002, 11003, 11004, 11100, 11101, 11102, 11103, 11104 };

        /// <summary>
        /// Determines if a URI requires path style addressing.
        /// </summary>
        /// <param name="uri">The URI to check.</param>
        /// <returns>Returns <c>true</c> if the Uri uses path style addressing; otherwise, <c>false</c>.</returns>
        internal static bool UsePathStyleAddressing(Uri uri)
        {
            Guard.AgainstNull(nameof(uri), uri);

            if (uri.HostNameType != UriHostNameType.Dns)
            {
                return true;
            }

            return PathStylePorts.Contains(uri.Port);
        }

        /// <summary>
        /// Returns the URI for the storage account endpoint at the specified location.
        /// </summary>
        /// <param name="location">A <see cref="StorageLocation"/> enumeration value.</param>
        /// <returns>The <see cref="Uri"/> for the endpoint at the the specified location.</returns>
        public Uri GetUri(StorageLocation location) => location switch
        {
            StorageLocation.Primary => PrimaryUri,
            StorageLocation.Secondary => SecondaryUri,
            _ => null,
        };

        internal bool ValidateLocationMode(LocationMode mode) =>
            mode switch
            {
                LocationMode.PrimaryOnly => PrimaryUri != null,
                LocationMode.SecondaryOnly => SecondaryUri != null,
                LocationMode.PrimaryThenSecondary => (PrimaryUri != null) && (SecondaryUri != null),
                LocationMode.SecondaryThenPrimary => (PrimaryUri != null) && (SecondaryUri != null),
                _ => (PrimaryUri != null) && (SecondaryUri != null),
            };

        /// <summary>
        /// Returns a <see cref="string"/> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="string"/> that represents this instance.
        /// </returns>
        public override string ToString() => string.Format(
            CultureInfo.InvariantCulture,
            "Primary = '{0}'; Secondary = '{1}'",
            PrimaryUri,
            SecondaryUri);

        /// <summary>
        /// Returns a hash code for this instance.
        /// </summary>
        /// <returns>
        /// A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table.
        /// </returns>
        public override int GetHashCode()
        {
            int hash1 = PrimaryUri != null ? PrimaryUri.GetHashCode() : 0;
            int hash2 = SecondaryUri != null ? SecondaryUri.GetHashCode() : 0;
            return hash1 ^ hash2;
        }

        /// <summary>
        /// Determines whether the specified <see cref="object"/> is equal to this instance.
        /// </summary>
        /// <param name="obj">The <see cref="object"/> to compare with this instance.</param>
        /// <returns><c>true</c> if the specified <see cref="object"/> is equal to this instance; otherwise, <c>false</c>.</returns>
        public override bool Equals(object obj) => Equals(obj as StorageUri);

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <param name="other">An object to compare with this object.</param>
        /// <returns><c>true</c> if the current object is equal to the <paramref name="other"/> parameter; otherwise, <c>false</c>.</returns>
        public bool Equals(StorageUri other) =>
            (other != null) &&
            (PrimaryUri == other.PrimaryUri) &&
            (SecondaryUri == other.SecondaryUri);

        /// <summary>
        /// Compares two <see cref="StorageUri"/> objects for equivalency.
        /// </summary>
        /// <param name="uri1">The first <see cref="StorageUri"/> object to compare.</param>
        /// <param name="uri2">The second <see cref="StorageUri"/> object to compare.</param>
        /// <returns><c>true</c> if the <see cref="StorageUri"/> objects have equivalent values; otherwise, <c>false</c>.</returns>
        public static bool operator ==(StorageUri uri1, StorageUri uri2)
        {
            if (ReferenceEquals(uri1, uri2))
            {
                return true;
            }

            if (uri1 is null)
            {
                return false;
            }

            return uri1.Equals(uri2);
        }

        /// <summary>
        /// Compares two <see cref="StorageUri"/> objects for non-equivalency.
        /// </summary>
        /// <param name="uri1">The first <see cref="StorageUri"/> object to compare.</param>
        /// <param name="uri2">The second <see cref="StorageUri"/> object to compare.</param>
        /// <returns><c>true</c> if the <see cref="StorageUri"/> objects have non-equivalent values; otherwise, <c>false</c>.</returns>
        public static bool operator !=(StorageUri uri1, StorageUri uri2) => !(uri1 == uri2);

        static void AssertAbsoluteUri(Uri uri)
        {
            if ((uri != null) && !uri.IsAbsoluteUri)
            {
                string errorMessage = string.Format(CultureInfo.InvariantCulture, ConnectionStringParser.RelativeAddressNotPermitted, uri.ToString());
                throw new ArgumentException(errorMessage, nameof(uri));
            }
        }
    }

    /// <summary>
    /// Represents a storage service location.
    /// </summary>
    enum StorageLocation
    {
        /// <summary>
        /// Primary storage service location.
        /// </summary>
        Primary,

        /// <summary>
        /// Secondary storage service location.
        /// </summary>
        Secondary,
    }

    /// <summary>
    /// Specifies the location mode to indicate which location should receive the request.
    /// </summary>
    enum LocationMode
    {
        /// <summary>
        /// Requests are always sent to the primary location.
        /// </summary>
        /// <remarks>
        /// If this value is used for requests that only work against a secondary location
        /// (GetServiceStats, for example), the request will fail in the client.
        /// </remarks>
        PrimaryOnly,

        /// <summary>
        /// Requests are always sent to the primary location first. If a request fails, it is sent to the secondary location.
        /// </summary>
        /// <remarks>
        /// If this value is used for requests that are only valid against one location, the client will
        /// only target the allowed location.
        /// </remarks>
        PrimaryThenSecondary,

        /// <summary>
        /// Requests are always sent to the secondary location.
        /// </summary>
        /// <remarks>
        /// If this value is used for requests that only work against a primary location
        /// (create, modify, and delete APIs), the request will fail in the client.
        /// </remarks>
        SecondaryOnly,

        /// <summary>
        /// Requests are always sent to the secondary location first. If a request fails, it is sent to the primary location.
        /// </summary>
        /// <remarks>
        /// If this value is used for requests that are only valid against one location, the client will
        /// only target the allowed location.
        /// </remarks>
        SecondaryThenPrimary,
    }
}
