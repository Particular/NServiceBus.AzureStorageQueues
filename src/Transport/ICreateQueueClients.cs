namespace NServiceBus.Azure.Transports.WindowsAzureStorageQueues
{
    using Microsoft.WindowsAzure.Storage.Queue;

    public interface ICreateQueueClients
    {
        CloudQueueClient Create(ConnectionString connectionStringValue);
    }
}