namespace NServiceBus.Transport.AzureStorageQueues
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    class DisabledNativeDelayDelivery : INativeDelayDelivery
    {
        public Task Start() => Task.CompletedTask;

        public Task Stop() => Task.CompletedTask;

        public Task<bool> ShouldDispatch(UnicastTransportOperation operation, CancellationToken cancellationToken)
        {
            var constraints = operation.DeliveryConstraints;
            var delay = NativeDelayDelivery.GetVisibilityDelay(constraints);
            if (delay != null)
            {
                throw new Exception("Cannot delay delivery of messages when delayed delivery has been disabled. Remove the 'endpointConfiguration.UseTransport<AzureStorageQueues>.DelayedDelivery().DisableDelayedDelivery()' configuration to re-enable delayed delivery.");
            }

            return Task.FromResult(true);
        }
    }
}