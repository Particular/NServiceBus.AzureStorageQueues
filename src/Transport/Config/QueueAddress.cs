namespace NServiceBus.Transport.AzureStorageQueues
{
    using System;
    using NServiceBus.Transport.AzureStorageQueues.Utils;

    readonly struct QueueAddress : IEquatable<QueueAddress>
    {
        public QueueAddress(string queueName, string alias)
        {
            if (string.IsNullOrWhiteSpace(queueName))
            {
                throw new ArgumentException("Queue name cannot be null nor empty", nameof(queueName));
            }

            QueueName = queueName;

            Alias = alias ?? throw new ArgumentNullException(nameof(alias));
        }

        public readonly string QueueName;
        public readonly string Alias;

        public bool Equals(QueueAddress other) =>
            string.Equals(QueueName, other.QueueName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(Alias, other.Alias, StringComparison.OrdinalIgnoreCase);

        public static QueueAddress Parse(string inputQueue, bool allowConnectionStringForBackwardCompatibility = false) =>
            TryParseInternal(inputQueue, allowConnectionStringForBackwardCompatibility, out var q, error => throw new FormatException(error))
                ? q.Value
                : throw new FormatException($"Can not parse '{inputQueue}' as an address");

        public static bool TryParse(string inputQueue, bool allowConnectionStringForBackwardCompatibility, out QueueAddress? queue) =>
            TryParseInternal(inputQueue, allowConnectionStringForBackwardCompatibility, out queue);

        static bool TryParseInternal(string inputQueue, bool allowConnectionStringForBackwardCompatibility, out QueueAddress? queue, Action<string> onError = null)
        {
            if (inputQueue == null)
            {
                onError?.Invoke("inputQueue cannot be null");
                queue = null;
                return false;
            }

            var index = inputQueue.IndexOf(Separator, StringComparison.Ordinal);
            if (index < 0)
            {
                if (string.IsNullOrWhiteSpace(inputQueue))
                {
                    onError?.Invoke("inputQueue cannot be empty");
                    queue = null;
                    return false;
                }

                queue = new QueueAddress(inputQueue, string.Empty);
                return true;
            }

            var queueName = inputQueue.Substring(0, index);

            if (string.IsNullOrWhiteSpace(queueName))
            {
                onError?.Invoke("Queue name cannot be empty");
                queue = null;
                return false;
            }

            var connectionStringOrAlias = inputQueue.Substring(index + 1);

            if (connectionStringOrAlias.IsValidAzureConnectionString() && allowConnectionStringForBackwardCompatibility == false)
            {
                onError?.Invoke("An attempt to use an address with a connection string using the 'destination@connectionstring' format was detected." +
                               " Only aliases are allowed. Provide an alias for the storage account.");
                queue = null;
                return false;
            }

            queue = new QueueAddress(queueName, connectionStringOrAlias);
            return true;
        }

        public override bool Equals(object obj)
        {
            if (obj is null)
            {
                return false;
            }
            return obj is QueueAddress address && Equals(address);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (QueueName.GetHashCode() * 397) ^ Alias.GetHashCode();
            }
        }

        public override string ToString() => HasNoAlias ? QueueName : $"{QueueName}{Separator}{Alias}";

        public bool HasNoAlias => Alias == string.Empty;

        public const string Separator = "@";
    }
}