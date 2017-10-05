namespace NServiceBus.AzureStorageQueues.Config
{
    using System;

    struct QueueAddress : IEquatable<QueueAddress>
    {
        public QueueAddress(string queueName, string storageAccount)
        {
            if (IsQueueNameValid(queueName) == false)
            {
                throw new ArgumentException("Queue name cannot be null nor empty", nameof(queueName));
            }

            if (storageAccount == null)
            {
                throw new ArgumentNullException(nameof(storageAccount));
            }

            QueueName = queueName;
            StorageAccount = storageAccount;
        }

        public readonly string QueueName;
        public readonly string StorageAccount;

        public bool Equals(QueueAddress other)
        {
            return string.Equals(QueueName, other.QueueName, StringComparison.OrdinalIgnoreCase) && string.Equals(StorageAccount, other.StorageAccount, StringComparison.OrdinalIgnoreCase);
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

                queue = new QueueAddress(inputQueue, DefaultStorageAccountAlias);
                return true;
            }

            var queueName = inputQueue.Substring(0, index);

            if (IsQueueNameValid(queueName) == false)
            {
                queue = null;
                return false;
            }

            var storageAccount = inputQueue.Substring(index + 1);
            queue = new QueueAddress(queueName, storageAccount);
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
                return (QueueName.GetHashCode()*397) ^ StorageAccount.GetHashCode();
            }
        }
        
        public override string ToString()
        {
            return IsAccountDefault ? QueueName : $"{QueueName}@{StorageAccount}";
        }

        public bool IsAccountDefault => StorageAccount == DefaultStorageAccountAlias;

        public const string DefaultStorageAccountAlias = "default";
        public const string Separator = "@";
    }
}