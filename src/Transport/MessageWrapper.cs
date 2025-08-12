namespace NServiceBus.Azure.Transports.WindowsAzureStorageQueues
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    [Serializable]
    public class MessageWrapper : IMessage
    {
        public string IdForCorrelation { get; set; }

        public string Id { get; set; }

        public MessageIntent MessageIntent { get; set; }

        public string ReplyToAddress { get; set; }

        /// <summary>
        /// Legacy property for backwards compatibility.
        /// The transport does not does use this property for any reason, but removing it could cause serialization problems.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public TimeSpan TimeToBeReceived { get; set; }

        public Dictionary<string, string> Headers { get; set; }

        public byte[] Body { get; set; }

        public string CorrelationId { get; set; }

        public bool Recoverable { get; set; }
    }
}
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member