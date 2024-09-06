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

    public override Task<Response> DeleteMessageAsync(string messageId, string popReceipt,
        CancellationToken cancellationToken = new CancellationToken())
    {
        deletedMessages.Add((messageId, popReceipt));
        return Task.FromResult(default(Response));
    }

    readonly List<(string messageId, string popReceipt)> deletedMessages = [];
}