namespace NServiceBus.Transport.AzureStorageQueues.Tests;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using global::Azure;
using global::Azure.Storage.Queues;

public class FakeQueueClient() : QueueClient(new Uri("http://localhost"))
{
    public IReadOnlyCollection<(string messageId, string popReceipt)> DeletedMessages => deletedMessages;

    public Func<Task<Response>> DeleteMessageCallback = () => Task.FromResult(default(Response));

    public override async Task<Response> DeleteMessageAsync(string messageId, string popReceipt,
        CancellationToken cancellationToken = default)
    {
        var response = await DeleteMessageCallback();
        deletedMessages.Add((messageId, popReceipt));
        return response;
    }

    readonly List<(string messageId, string popReceipt)> deletedMessages = [];
}