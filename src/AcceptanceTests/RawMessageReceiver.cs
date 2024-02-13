namespace NServiceBus.Transport.AzureStorageQueues.AcceptanceTests
{
    using System;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Azure.Storage.Queues;
    using global::Azure.Storage.Queues.Models;
    using NServiceBus.Azure.Transports.WindowsAzureStorageQueues;
    using NUnit.Framework;

    class RawMessageReceiver
    {
        public static Task<MessageWrapper> Receive(string connectionString, string queueName, string testRunId, CancellationToken cancellationToken = default)
        {
            var queueClient = new QueueClient(connectionString, queueName);

            return Receive(queueClient, testRunId, cancellationToken);
        }

        public static async Task<MessageWrapper> Receive(QueueClient queueClient, string testRunId, CancellationToken cancellationToken = default)
        {
            do
            {
                cancellationToken.ThrowIfCancellationRequested();

                QueueMessage[] rawMessages = await queueClient.ReceiveMessagesAsync(1, cancellationToken: cancellationToken);

                if (rawMessages.Length == 0)
                {
                    Assert.Fail("No message in the queue for RawMessageReceiver to pick up.");
                }

                var rawMessage = rawMessages[0];
                var response = await queueClient.DeleteMessageAsync(rawMessage.MessageId, rawMessage.PopReceipt, cancellationToken);

                Assert.That(response, Is.Not.Null);
                Assert.That(response.IsError, Is.False);

                var envelope = JsonSerializer.Deserialize<MessageWrapper>(Convert.FromBase64String(rawMessage.MessageText));

                if (envelope.Headers.TryGetValue(TestIndependence.HeaderName, out var runId))
                {
                    if (testRunId == runId)
                    {
                        return envelope;
                    }
                }
            }
            while (true);
        }
    }
}
