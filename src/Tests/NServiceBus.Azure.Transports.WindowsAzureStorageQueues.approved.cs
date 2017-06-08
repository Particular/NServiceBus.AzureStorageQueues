﻿[assembly: System.Runtime.CompilerServices.InternalsVisibleToAttribute(@"NServiceBus.AzureStorageQueues.Tests, PublicKey=0024000004800000940000000602000000240000525341310004000001000100DDE965E6172E019AC82C2639FFE494DD2E7DD16347C34762A05732B492E110F2E4E2E1B5EF2D85C848CCFB671EE20A47C8D1376276708DC30A90FF1121B647BA3B7259A6BC383B2034938EF0E275B58B920375AC605076178123693C6C4F1331661A62EBA28C249386855637780E3FF5F23A6D854700EAA6803EF48907513B92")]
[assembly: System.Runtime.InteropServices.ComVisibleAttribute(false)]
[assembly: System.Runtime.Versioning.TargetFrameworkAttribute(".NETFramework,Version=v4.5.2", FrameworkDisplayName=".NET Framework 4.5.2")]

namespace NServiceBus
{
    
    public class AccountRoutingSettings
    {
        public void AddAccount(string alias, string connectionString) { }
    }
    public class AzureStorageQueueTransport : NServiceBus.Transport.TransportDefinition, NServiceBus.Routing.IMessageDrivenSubscriptionTransport
    {
        public AzureStorageQueueTransport() { }
        public override string ExampleConnectionStringForErrorMessage { get; }
        public override bool RequiresConnectionString { get; }
        public override NServiceBus.Transport.TransportInfrastructure Initialize(NServiceBus.Settings.SettingsHolder settings, string connectionString) { }
    }
    public class static AzureStorageTransportAddressingExtensions
    {
        public static NServiceBus.AccountRoutingSettings AccountRouting(this NServiceBus.TransportExtensions<NServiceBus.AzureStorageQueueTransport> transportExtensions) { }
        public static NServiceBus.TransportExtensions<NServiceBus.AzureStorageQueueTransport> DefaultAccountAlias(this NServiceBus.TransportExtensions<NServiceBus.AzureStorageQueueTransport> transportExtensions, string alias) { }
        public static NServiceBus.TransportExtensions<NServiceBus.AzureStorageQueueTransport> UseAccountAliasesInsteadOfConnectionStrings(this NServiceBus.TransportExtensions<NServiceBus.AzureStorageQueueTransport> config) { }
    }
    public class static AzureStorageTransportExtensions
    {
        public static NServiceBus.TransportExtensions<NServiceBus.AzureStorageQueueTransport> BatchSize(this NServiceBus.TransportExtensions<NServiceBus.AzureStorageQueueTransport> config, int value) { }
        public static NServiceBus.TransportExtensions<NServiceBus.AzureStorageQueueTransport> DegreeOfReceiveParallelism(this NServiceBus.TransportExtensions<NServiceBus.AzureStorageQueueTransport> config, int degreeOfReceiveParallelism) { }
        public static NServiceBus.DelayedDeliverySettings DelayedDelivery(this NServiceBus.TransportExtensions<NServiceBus.AzureStorageQueueTransport> config) { }
        public static NServiceBus.TransportExtensions<NServiceBus.AzureStorageQueueTransport> MaximumWaitTimeWhenIdle(this NServiceBus.TransportExtensions<NServiceBus.AzureStorageQueueTransport> config, System.TimeSpan value) { }
        public static NServiceBus.TransportExtensions<NServiceBus.AzureStorageQueueTransport> MessageInvisibleTime(this NServiceBus.TransportExtensions<NServiceBus.AzureStorageQueueTransport> config, System.TimeSpan value) { }
        public static NServiceBus.TransportExtensions<NServiceBus.AzureStorageQueueTransport> PeekInterval(this NServiceBus.TransportExtensions<NServiceBus.AzureStorageQueueTransport> config, System.TimeSpan value) { }
        public static NServiceBus.TransportExtensions<NServiceBus.AzureStorageQueueTransport> SerializeMessageWrapperWith<TSerializationDefinition>(this NServiceBus.TransportExtensions<NServiceBus.AzureStorageQueueTransport> config)
            where TSerializationDefinition : NServiceBus.Serialization.SerializationDefinition, new () { }
        public static NServiceBus.TransportExtensions<NServiceBus.AzureStorageQueueTransport> UnwrapMessagesWith(this NServiceBus.TransportExtensions<NServiceBus.AzureStorageQueueTransport> config, System.Func<Microsoft.WindowsAzure.Storage.Queue.CloudQueueMessage, NServiceBus.Azure.Transports.WindowsAzureStorageQueues.MessageWrapper> unwrapper) { }
        public static NServiceBus.TransportExtensions<NServiceBus.AzureStorageQueueTransport> UseSha1ForShortening(this NServiceBus.TransportExtensions<NServiceBus.AzureStorageQueueTransport> config) { }
    }
    public class DelayedDeliverySettings
    {
        public DelayedDeliverySettings() { }
        public void DisableTimeoutManager() { }
        public void TableName(string timeoutTableName) { }
    }
}
namespace NServiceBus.Azure.Transports.WindowsAzureStorageQueues
{
    
    public class MessageWrapper : NServiceBus.IMessage
    {
        public MessageWrapper() { }
        public byte[] Body { get; set; }
        public string CorrelationId { get; set; }
        public System.Collections.Generic.Dictionary<string, string> Headers { get; set; }
        public string Id { get; set; }
        public string IdForCorrelation { get; set; }
        public NServiceBus.MessageIntentEnum MessageIntent { get; set; }
        public bool Recoverable { get; set; }
        public string ReplyToAddress { get; set; }
        public System.TimeSpan TimeToBeReceived { get; set; }
    }
}