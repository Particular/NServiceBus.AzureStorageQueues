# NServiceBus.AzureStorageQueues

NServiceBus.AzureStorageQueues enables the use of [Azure Storage Queues](https://learn.microsoft.com/en-us/azure/storage/queues/storage-queues-introduction) as the underlying transport used by NServiceBus.

It is part of the [Particular Service Platform](https://particular.net/service-platform), which includes [NServiceBus](https://particular.net/nservicebus) and tools to build, monitor, and debug distributed systems.

See the [Azure Storage Queues Transport documentation](https://docs.particular.net/nservicebus/azure-storage-queues/) for more details on how to use it.

## Running tests locally

To run the tests locally, add a new environment variable `AzureStorageQueueTransport_ConnectionString` containing a connection string to your Azure storage account or the connection string `UseDevelopmentStorage=true` to use the [Azurite emulator](https://docs.microsoft.com/en-us/azure/storage/common/storage-use-azurite) (ensure it is started before you run the tests).

Additionally, [Microsoft Azure Storage Explorer](https://azure.microsoft.com/en-us/products/storage/storage-explorer) is an useful free tool that can allow you to view and manage the contents of the Azurite emulator as well as Azure Storage accounts in the cloud.
