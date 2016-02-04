namespace NServiceBus.Azure.Transports.WindowsAzureStorageQueues
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.Serialization;

    [Serializable]
    [DataContract(Namespace = "http://tempuri.net/NServiceBus.Azure.Transports.WindowsAzureStorageQueues")]
    public class MessageWrapper
    {
        [DataMember(Order = 1, EmitDefaultValue = false)]
        public string IdForCorrelation { get; set; }

        [DataMember(Order = 2, EmitDefaultValue = false)]
        public string Id { get; set; }

        [DataMember(Order = 3, EmitDefaultValue = false)]
        public MessageIntentEnum MessageIntent { get; set; }

        [DataMember(Order = 4, EmitDefaultValue = false)]
        public string ReplyToAddress { get; set; }

        [DataMember(Order = 5, EmitDefaultValue = false)]
        public TimeSpan TimeToBeReceived { get; set; }

        [DataMember(Order = 6, EmitDefaultValue = false)]
        public HeadersCollection Headers { get; set; }

        [DataMember(Order = 7, EmitDefaultValue = false)]
        public byte[] Body { get; set; }

        [DataMember(Order = 8, EmitDefaultValue = false)]
        public string CorrelationId { get; set; }

        [DataMember(Order = 9, EmitDefaultValue = false)]
        public bool Recoverable { get; set; }
    }

    [Serializable]
    [CollectionDataContract
        (Name = "Headers",
        ItemName = "NServiceBus.KeyValuePairOfStringAndString",
        KeyName = "Key",
        ValueName = "Value",
        Namespace = "")]
    public class HeadersCollection : Dictionary<string, string>
    {
        public HeadersCollection()
        {
        }

        public HeadersCollection(IDictionary<string, string> dictionary) : base(dictionary)
        {
        }
    }
}