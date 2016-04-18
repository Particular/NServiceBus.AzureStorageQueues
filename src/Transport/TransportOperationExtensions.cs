namespace NServiceBus.Azure.Transports.WindowsAzureStorageQueues
{
    using System;
    using System.Linq;
    using DeliveryConstraints;
    using NServiceBus.Transports;
    using Performance.TimeToBeReceived;

    static class TransportOperationExtensions
    {
        public static TimeSpan? GetTimeToBeReceived(this UnicastTransportOperation operation)
        {
            return operation.GetDeliveryConstraint<DiscardIfNotReceivedBefore>()?.MaxTime;
        }

        public static T GetDeliveryConstraint<T>(this IOutgoingTransportOperation operation)
            where T : DeliveryConstraint
        {
            return operation.DeliveryConstraints.OfType<T>().FirstOrDefault();
        }
    }
}