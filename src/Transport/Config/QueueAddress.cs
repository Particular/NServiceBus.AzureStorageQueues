namespace NServiceBus.AzureStorageQueues.Config
{
    using System;

    class QueueAddress : IEquatable<QueueAddress>
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

        public string QueueName { get; }
        public string StorageAccount { get; }

        public bool Equals(QueueAddress other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }
            if (ReferenceEquals(this, other))
            {
                return true;
            }
            return string.Equals(QueueName, other.QueueName) && string.Equals(StorageAccount, other.StorageAccount);
        }

        static bool IsQueueNameValid(string queueName)
        {
            return string.IsNullOrWhiteSpace(queueName) == false;
        }

        public static QueueAddress Parse(string value)
        {
            QueueAddress q;
            if (TryParse(value, out q) == false)
            {
                throw new ArgumentException("Value cannot be parsed", nameof(value));
            }

            return q;
        }

        public static bool TryParse(string inputQueue, out QueueAddress queue)
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
            if (ReferenceEquals(this, obj))
            {
                return true;
            }
            return obj is QueueAddress && Equals((QueueAddress) obj);
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

        public const string DefaultStorageAccountAlias = "";
        public const string Separator = "@";
    }
}