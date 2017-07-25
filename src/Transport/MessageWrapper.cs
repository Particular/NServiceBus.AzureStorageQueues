namespace NServiceBus.Azure.Transports.WindowsAzureStorageQueues
{
    using System;
    using System.Collections.Generic;

    [Serializable]
    public class MessageWrapper : IMessage
    {
        public string IdForCorrelation { get; set; }

        public string Id { get; set; }

        public MessageIntentEnum MessageIntent { get; set; }

        public string ReplyToAddress { get; set; }

        [ObsoleteEx(Message = "Unnecessary property for MessageWrapper.", TreatAsErrorFromVersion = "8", RemoveInVersion = "9")]
        public TimeSpan TimeToBeReceived { get; set; }

        public Dictionary<string, string> Headers { get; set; }

        public byte[] Body { get; set; }

        public string CorrelationId { get; set; }

        public bool Recoverable { get; set; }
    }
}