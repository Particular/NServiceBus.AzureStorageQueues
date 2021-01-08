namespace NServiceBus.Transport.AzureStorageQueues
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using global::Azure;
    using global::Azure.Storage.Queues;
    using global::Azure.Storage.Queues.Models;
    using Logging;
    using Transport;

    /// <summary>
    /// Creates the queues. Note that this class will only be invoked when running the windows host and not when running in
    /// the fabric
    /// </summary>
    class AzureMessageQueueCreator : ICreateQueues
    {
        public AzureMessageQueueCreator(IQueueServiceClientProvider queueServiceClientProviderProvider, QueueAddressGenerator addressGenerator)
        {
            queueServiceClient = queueServiceClientProviderProvider.Client;
            this.addressGenerator = addressGenerator;
        }

        public Task CreateQueueIfNecessary(QueueBindings queueBindings, string identity)
        {
            var addresses = queueBindings.ReceivingAddresses.Union(queueBindings.SendingAddresses);
            return Task.WhenAll(addresses.Select(CreateQueue));
        }

        async Task CreateQueue(string address)
        {
            Logger.DebugFormat("Creating queue '{0}'", address);
            var queueName = addressGenerator.GetQueueName(address);
            try
            {
                var queue = queueServiceClient.GetQueueClient(queueName);
                await queue.CreateIfNotExistsAsync().ConfigureAwait(false);
            }
            catch (RequestFailedException ex)
            {
                //// https://msdn.microsoft.com/en-us/library/azure/dd179446.aspx

                if (ex.Status == 409)
                {
                    if (ex.ErrorCode == QueueErrorCode.QueueAlreadyExists)
                    {
                        return;
                    }
                }

                // TODO: should we throw with Message or Message+ErrorCode
                throw new RequestFailedException($"Failed to create queue: {queueName}, because {ex.Status}-{ex.Message}.", ex);
            }
            catch (Exception ex)
            {
                throw new RequestFailedException($"Failed to create queue: {queueName}, because {ex.Message}.", ex);
            }
        }

        QueueAddressGenerator addressGenerator;
        QueueServiceClient queueServiceClient;
        static readonly ILog Logger = LogManager.GetLogger<AzureMessageQueueCreator>();
    }
}