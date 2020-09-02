namespace NServiceBus.AzureStorageQueues.AcceptanceTests
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Table;

    public class CaptureSendingRequests : IDisposable
    {
        readonly ConcurrentQueue<RequestEventArgs> events = new ConcurrentQueue<RequestEventArgs>();
        readonly EventHandler<RequestEventArgs> action;

        public CaptureSendingRequests()
        {
            action = (sender, args) => events.Enqueue(args);
            OperationContext.GlobalSendingRequest += action;
        }

        public IEnumerable<RequestEventArgs> Events => events;

        public void Dispose()
        {
            OperationContext.GlobalSendingRequest -= action;
        }
    }
}