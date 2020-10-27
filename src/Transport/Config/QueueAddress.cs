using Microsoft.Azure.Cosmos.Table;

namespace NServiceBus.Transport.AzureStorageQueues
{
    using System;

    struct QueueAddress : IEquatable<QueueAddress>
    {
        public QueueAddress(string queueName, string alias)
        {
            if (IsQueueNameValid(queueName) == false)
            {
                throw new ArgumentException("Queue name cannot be null nor empty", nameof(queueName));
            }

            QueueName = queueName;

            Alias = alias ?? throw new ArgumentNullException(nameof(alias));
        }

        public readonly string QueueName;
        public readonly string Alias;

        public bool Equals(QueueAddress other)
        {
            return string.Equals(QueueName, other.QueueName, StringComparison.OrdinalIgnoreCase) && string.Equals(Alias, other.Alias, StringComparison.OrdinalIgnoreCase);
        }

        static bool IsQueueNameValid(string queueName)
        {
            return string.IsNullOrWhiteSpace(queueName) == false;
        }

        public static QueueAddress Parse(string value)
        {
            if (TryParse(value, out var q) == false)
            {
                throw new ArgumentException("Value cannot be parsed", nameof(value));
            }

            return q.Value;
        }

        public static bool TryParse(string inputQueue, out QueueAddress? queue)
        {
            if (inputQueue == null)
            {
                queue = null;
                return false;
            }

            var index = inputQueue.IndexOf(Separator, StringComparison.Ordinal);
            if (index < 0)
            {
                if (IsQueueNameValid(inputQueue) == false)
                {
                    queue = null;
                    return false;
                }

                queue = new QueueAddress(inputQueue, "");
                return true;
            }

            var queueName = inputQueue.Substring(0, index);

            if (IsQueueNameValid(queueName) == false)
            {
                queue = null;
                return false;
            }

            var connectionStringOrAlias = inputQueue.Substring(index + 1);

            if (CloudStorageAccount.TryParse(connectionStringOrAlias, out _))
            {
                const string message =
                    "An attempt to use an address with a connection string using the 'destination@connecitonstring' format was detected."
                    + " Only aliases are allowed. Provide an alias for the storage account.";
                throw new Exception(message);
            }

            queue = new QueueAddress(queueName, connectionStringOrAlias);
            return true;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }
            return obj is QueueAddress address && Equals(address);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (QueueName.GetHashCode()*397) ^ Alias.GetHashCode();
            }
        }

        public override string ToString()
        {
            return HasNoAlias ? QueueName : $"{QueueName}@{Alias}";
        }

        public bool HasNoAlias => Alias == string.Empty;

        public const string Separator = "@";
    }
}