namespace NServiceBus.Azure.Transports.WindowsAzureStorageQueues
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading.Tasks;
    using DelayedDelivery;
    using DeliveryConstraints;
    using Features;
    using Microsoft.WindowsAzure.Storage.Queue;
    using Pipeline;
    using Routing;
    using Transport;

    class NativeDelayDeliveryFeature : Feature
    {
        const string HeaderName = "ASQ.DelayedDeliveryDateTimeOffset";
        const string DateTimeOffsetFormat = "O";

        /// <summary>
        /// make the delay less, to do not expire message automatically
        /// </summary>
        static readonly TimeSpan MaxVisibilityDelay = CloudQueueMessage.MaxTimeToLive - TimeSpan.FromDays(1);
        
        protected override void Setup(FeatureConfigurationContext context)
        {
            var localAddress = context.Settings.LocalAddress();
            context.Pipeline.Register(builder => new NativeDelayDeliveryBehavior(builder.Build<IDispatchMessages>(), localAddress), "Azure Storage Queue transport native delay delivery behavior");
        }

        public static TimeSpan? GetVisibilityDelay(IOutgoingTransportOperation operation)
        {
            var visbilityDelay = GetInitialVisbilityDelay(operation);
            if (visbilityDelay == null)
            {
                return null;
            }
            
            var value = visbilityDelay.Value;

            // set the header
            operation.Message.Headers[HeaderName] = (DateTimeOffset.UtcNow + value).ToString(DateTimeOffsetFormat, CultureInfo.InvariantCulture);
            if (value >= MaxVisibilityDelay)
            {
                // make the message reappear after MaxVisibilityDelay
                return MaxVisibilityDelay;
            }
            return visbilityDelay;
        }

        static bool TryGetNextVisibilityTime(ITransportReceiveContext context, out DateTimeOffset nextVisibilityTime)
        {
            nextVisibilityTime = DateTimeOffset.MinValue;
            string value;
            if (context.Message.Headers.TryGetValue(HeaderName, out value))
            {
                return DateTimeOffset.TryParseExact(value, DateTimeOffsetFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out nextVisibilityTime);
            }
            
            return false;
        }

        static TimeSpan? GetInitialVisbilityDelay(IOutgoingTransportOperation operation)
        {
            var deliveryConstraint = operation.DeliveryConstraints.FirstOrDefault(d => d is DelayedDeliveryConstraint);

            var value = TimeSpan.Zero;
            if (deliveryConstraint != null)
            {
                var delay = deliveryConstraint as DelayDeliveryWith;
                if (delay != null)
                {
                    value = delay.Delay;
                    return value;
                }
                else
                {
                    var exact = deliveryConstraint as DoNotDeliverBefore;
                    if (exact != null)
                    {
                        value = DateTimeOffset.Now - exact.At;
                    }
                }
            }

            return value <= TimeSpan.Zero ? (TimeSpan?) null : value;

        }

        class NativeDelayDeliveryBehavior : Behavior<ITransportReceiveContext>
        {
            readonly IDispatchMessages dispatcher;
            readonly string localAddress;

            public NativeDelayDeliveryBehavior(IDispatchMessages dispatcher, string localAddress)
            {
                this.dispatcher = dispatcher;
                this.localAddress = localAddress;
            }

            public override Task Invoke(ITransportReceiveContext context, Func<Task> next)
            {
                var now = DateTimeOffset.UtcNow;
                DateTimeOffset nextVisibilityTime;
                if (TryGetNextVisibilityTime(context, out nextVisibilityTime))
                {
                    var diff = nextVisibilityTime - now;
                    
                    // if next in the past, dispatch now
                    if (diff < TimeSpan.Zero)
                    {
                        return next();
                    }

                    // TODO: what with other constraints like sending a timeout with TimeSpan.FromMinutes(5) which should be expired after TimeSpan.FromMinutes(6) when not delivered. This should be copied to the new message

                    var msg = context.Message;
                    var outgoingMessage = new OutgoingMessage(msg.MessageId, msg.Headers, msg.Body);

                    return dispatcher.Dispatch(new TransportOperations(new TransportOperation(outgoingMessage, new UnicastAddressTag(localAddress), DispatchConsistency.Default, new List<DeliveryConstraint>
                    {
                        new DelayDeliveryWith(diff)
                    })), new TransportTransaction(), context.Extensions);
                }

                return next();
            }
        }
    }
}