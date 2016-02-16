namespace NServiceBus.Azure.Transports.WindowsAzureStorageQueues
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Queue;
    using NServiceBus.Transports;

    /// <summary>
    ///     Creates the queues. Note that this class will only be invoked when running the windows host and not when running in
    ///     the fabric
    /// </summary>
    public class AzureMessageQueueCreator : ICreateQueues
    {
        readonly QueueAddressGenerator addressGenerator;
        readonly CloudQueueClient client;
        readonly bool shouldCreateSendingQueues;

        public AzureMessageQueueCreator(CloudQueueClient client, QueueAddressGenerator addressGenerator, bool shouldCreateSendingQueues)
        {
            this.client = client;
            this.addressGenerator = addressGenerator;
            this.shouldCreateSendingQueues = shouldCreateSendingQueues;
        }

        public async Task CreateQueueIfNecessary(QueueBindings queueBindings, string identity)
        {
            var addresses = queueBindings.ReceivingAddresses.ToArray();
            if (shouldCreateSendingQueues)
            {
                addresses = addresses.Concat(queueBindings.SendingAddresses).ToArray();
            }

            await Task.WhenAll(addresses.Select(CreateQueue)).ConfigureAwait(false);
        }

        private async Task CreateQueue(string address)
        {
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
                        await TaskEx.CompletedTask.ConfigureAwait(false);
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
    }
}