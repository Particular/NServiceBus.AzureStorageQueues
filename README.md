# Azure Storage Queues Transport for NServiceBus

The Azure Storage Queues transport for NServiceBus enables the use of the Azure Storage Queues service as the underlying transport used by NServiceBus.

## Documentation

* [Azure Transport](https://docs.particular.net/nservicebus/azure-storage-queues/)
* [Samples](https://docs.particular.net/samples/azure/storage-queues/)

## How to test locally

To run the tests locally, add a new environment variable `AzureStorageQueueTransport_ConnectionString` containing a connection string to your Azure storage account or the connection string `UseDevelopmentStorage=true` to use the [Azurite emulator](https://docs.microsoft.com/en-us/azure/storage/common/storage-use-azurite) (ensure it is started before you run the tests).
