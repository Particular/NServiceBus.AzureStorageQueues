﻿namespace NServiceBus
{
    using System;

    public partial class AzureStorageTransportExtensions
    {
        /// <summary>
        /// Overrides default Md5 shortener for creating queue names with Sha1 shortener.
        /// </summary>
        [ObsoleteEx(Message = "Azure Storage Queues transport is no longer shortening queue names and requires sanitization algorithm to be provided using configuration API.",
            ReplacementTypeOrMember = "transport.SanitizeQueueNamesWith(Func<string, string>)",
            TreatAsErrorFromVersion = "8.0", RemoveInVersion = "9.0")]
        public static TransportExtensions<AzureStorageQueueTransport> UseSha1ForShortening(this TransportExtensions<AzureStorageQueueTransport> config)
        {
            throw new NotImplementedException();
        }

    }
}