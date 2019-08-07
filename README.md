# Azure Storage Queues Transport for NServiceBus

The Azure Storage Queues transport for NServiceBus enables the use of the Azure Storage Queues service as the underlying transport used by NServiceBus.

## Documentation

* [Azure Transport](https://docs.particular.net/nservicebus/azure-storage-queues/)
* [Samples](https://docs.particular.net/samples/azure/storage-queues/)

## How to Test Locally

Follow these steps to run the acceptance tests locally:
* Add a new environment variable `Transport.UseSpecific` with the value `AzureStorageQueueTransport`
* Add a new environment variable `AzureStorageQueueTransport.ConnectionString` containing a connection string to your Azure storage account or use `UseDevelopmentStorage=true` to use the [Azure Storage Emulator](https://azure.microsoft.com/en-us/documentation/articles/storage-use-emulator/) (make sure to start it before you run the tests).
