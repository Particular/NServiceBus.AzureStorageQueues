namespace NServiceBus.AzureStorageQueue.Addressing
{
    using System.Collections.Generic;

    public interface IAccountPartitioningStrategy
    {
        IEnumerable<string> GetAccounts(PartitioningIntent partitioningIntent);
    }
}