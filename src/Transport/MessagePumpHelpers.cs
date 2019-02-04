namespace NServiceBus.Transport.AzureStorageQueues
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;

    static class MessagePumpHelpers
    {
        public static ReadOnlyCollection<ReceiverConfiguration> DetermineReceiverConfiguration(int? receiveBatchSize, int? degreeOfReceiveParallelism, int maximumConcurrency)
        {
            var maximumBatchSize = receiveBatchSize ?? DefaultConfigurationValues.DefaultBatchSize;
            int receiveParallelism;

            if (!degreeOfReceiveParallelism.HasValue)
            {
                if (maximumConcurrency <= maximumBatchSize)
                {
                    receiveParallelism = 1;
                }
                else
                {
                    receiveParallelism = Convert.ToInt32(Math.Ceiling(Convert.ToDouble(maximumConcurrency) / maximumBatchSize));
                }
            }
            else
            {
                receiveParallelism = degreeOfReceiveParallelism.Value;
            }

            var receiverConfigurations = new List<ReceiverConfiguration>();
            var remainder = maximumConcurrency;
            for (var i = 0; i < receiveParallelism; i++)
            {
                int? batchSizeForReceiver = null;
                // in case a user set the degree of parallelism try to determine the batch size in accordance to that but cap it at max DefaultConfigurationValues.DefaultBatchSize
                if (degreeOfReceiveParallelism.HasValue)
                {
                    // make sure we get 20% more to decrease latency and fetch overhead
                    batchSizeForReceiver = Math.Min(maximumBatchSize, Convert.ToInt32(Math.Max(1, Math.Ceiling(ReceiveBatchMultiplier * (maximumConcurrency / receiveParallelism)))));
                }

                // if the user has chosen a batch size value always respect that even when degree of parallelism has been set
                if (receiveBatchSize.HasValue)
                {
                    batchSizeForReceiver = receiveBatchSize.Value;
                }

                // if batchSizeForReceiver hasn't been calculated yet try to calculate it
                if (!batchSizeForReceiver.HasValue)
                {
                    if (remainder > maximumBatchSize)
                    {
                        batchSizeForReceiver = maximumBatchSize;
                    }
                    else
                    {
                        // make sure we get 20% more to decrease latency and fetch overhead
                        batchSizeForReceiver = Math.Min(maximumBatchSize, Convert.ToInt32(Math.Ceiling(Convert.ToDouble(remainder) * ReceiveBatchMultiplier)));
                    }

                    remainder -= maximumBatchSize;
                }

                receiverConfigurations.Add(new ReceiverConfiguration(batchSizeForReceiver.Value));
            }

            return new ReadOnlyCollection<ReceiverConfiguration>(receiverConfigurations);
        }

        static readonly double ReceiveBatchMultiplier = 1.2;
    }
}