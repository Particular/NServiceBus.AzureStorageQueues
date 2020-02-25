namespace NServiceBus.Transport.AzureStorageQueues
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Queue;
    using NServiceBus.Logging;
    using Transport;

    /// <summary>
    /// Creates the queues. Note that this class will only be invoked when running the windows host and not when running in
    /// the fabric
    /// </summary>
    class AzureMessageQueueCreator : ICreateQueues
    {
        public AzureMessageQueueCreator(CloudQueueClient client, QueueAddressGenerator addressGenerator)
        {
            this.client = client;
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
                var queue = client.GetQueueReference(queueName);
                await queue.CreateIfNotExistsAsync().ConfigureAwait(false);
            }
            catch (StorageException ex)
            {
                // https://msdn.microsoft.com/en-us/library/azure/dd179446.aspx
                var info = ex.RequestInformation;

                if (info.HttpStatusCode == 409)
                {
                    if (info.HttpStatusMessage == "QueueAlreadyExists")
                    {
                        return;
                    }
                }

                throw new StorageException($"Failed to create queue: {queueName}, because {info.HttpStatusCode}-{info.HttpStatusMessage}.", ex);
            }
            catch (Exception ex)
            {
                throw new StorageException($"Failed to create queue: {queueName}, because {ex.Message}.", ex);
            }
        }

        QueueAddressGenerator addressGenerator;
        CloudQueueClient client;
        static readonly ILog Logger = LogManager.GetLogger<AzureMessageQueueCreator>();
    }
}