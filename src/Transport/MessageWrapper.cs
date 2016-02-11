namespace NServiceBus.Azure.Transports.WindowsAzureStorageQueues
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.Serialization;
    using Newtonsoft.Json;

    [Serializable]
    [DataContract(Namespace = "http://tempuri.net/NServiceBus.Azure.Transports.WindowsAzureStorageQueues")]
    public class MessageWrapper : IMessage
    {
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)]
        [DataMember(Order = 1, EmitDefaultValue = false)]
        public string IdForCorrelation { get; set; }

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)]
        [DataMember(Order = 2, EmitDefaultValue = false)]
        public string Id { get; set; }

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)]
        [DataMember(Order = 3, EmitDefaultValue = false)]
        public MessageIntentEnum MessageIntent { get; set; }

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)]
        [DataMember(Order = 4, EmitDefaultValue = false)]
        public string ReplyToAddress { get; set; }

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)]
        [DataMember(Order = 5, EmitDefaultValue = false)]
        public TimeSpan TimeToBeReceived { get; set; }

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)]
        [DataMember(Order = 6, EmitDefaultValue = false)]
        public HeadersCollection Headers { get; set; }

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)]
        [DataMember(Order = 7, EmitDefaultValue = false)]
        public byte[] Body { get; set; }

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)]
        [DataMember(Order = 8, EmitDefaultValue = false)]
        public string CorrelationId { get; set; }

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)]
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

        protected HeadersCollection(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}