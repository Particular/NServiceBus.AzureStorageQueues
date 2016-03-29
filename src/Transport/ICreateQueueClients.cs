namespace NServiceBus.AzureStorageQueues
{
    using Microsoft.WindowsAzure.Storage.Queue;

    public interface ICreateQueueClients
    {
        CloudQueueClient Create(ConnectionString connectionStringValue);
    }
}