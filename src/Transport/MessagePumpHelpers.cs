namespace NServiceBus.Transport.AzureStorageQueues
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;

    static class MessagePumpHelpers
    {
        public static ReadOnlyCollection<ReceiverConfiguration> DetermineReceiverConfiguration(int? receiveBatchSize, int? degreeOfReceiveParallelism, int maximumConcurrency)
        {
            // When user overrides both parameters we stick with user choices
            if (receiveBatchSize.HasValue && degreeOfReceiveParallelism.HasValue)
            {
                return BuildConfiguration(degreeOfReceiveParallelism.Value * receiveBatchSize.Value, receiveBatchSize.Value);
            }

            var totalMessagesWithOverfetching = Math.Ceiling(ReceiveBatchMultiplier * maximumConcurrency);

            // By default we don't over-fetch
            if (receiveBatchSize.HasValue == false && degreeOfReceiveParallelism.HasValue == false)
            {
                return BuildConfiguration(Convert.ToInt32(totalMessagesWithOverfetching), DefaultConfigurationValues.DefaultBatchSize);
            }

            int maximumBatchSize;
            int receiveParallelism;

            // In other cases we adjust unset parameter to make sure we do not over-fetch by more than 20%
            if (receiveBatchSize.HasValue == false)
            {
                maximumBatchSize = DefaultConfigurationValues.DefaultBatchSize;
                receiveParallelism = degreeOfReceiveParallelism.Value;

                var batchSizeForReceiver = Math.Min(maximumBatchSize, Math.Max(1, Convert.ToInt32(Math.Ceiling(totalMessagesWithOverfetching / receiveParallelism))));

                return BuildConfiguration(receiveParallelism * batchSizeForReceiver, batchSizeForReceiver);
            }

            maximumBatchSize = receiveBatchSize.Value;
            receiveParallelism = Math.Max(1, Convert.ToInt32(Math.Ceiling(totalMessagesWithOverfetching / maximumBatchSize)));

            return BuildConfiguration(receiveParallelism * receiveBatchSize.Value, receiveBatchSize.Value);
        }

        static ReadOnlyCollection<ReceiverConfiguration> BuildConfiguration(int totalMessages, int maxBatchSize)
        {
            var configurations = new List<ReceiverConfiguration>();

            while (totalMessages > 0)
            {
                var batchSize = Math.Min(maxBatchSize, totalMessages);
                configurations.Add(new ReceiverConfiguration(batchSize));

                totalMessages -= batchSize;
            }

            return new ReadOnlyCollection<ReceiverConfiguration>(configurations);
        }

        // currently we are doing no over-fetching, this could be exposed in the future if needed.
        const double ReceiveBatchMultiplier = 1.0;
    }
}