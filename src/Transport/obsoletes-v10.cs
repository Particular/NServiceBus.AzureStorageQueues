#pragma warning disable 1591

namespace NServiceBus
{
    using System;

    static partial class AzureStorageTransportExtensions
    {
        /// <summary>
        /// Controls how long messages should be invisible to other callers when receiving messages from the queue
        /// </summary>
        [ObsoleteEx(
            Message = "Configure the transport via the TransportDefinition instance's properties",
            TreatAsErrorFromVersion = "10.0",
            RemoveInVersion = "11.0")]
        public static TransportExtensions<AzureStorageQueueTransport> MessageInvisibleTime(this TransportExtensions<AzureStorageQueueTransport> config, TimeSpan value)
        {
            throw new NotImplementedException();
        }
    }
}

#pragma warning restore 1591
