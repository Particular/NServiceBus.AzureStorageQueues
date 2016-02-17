namespace NServiceBus.Azure.Transports.WindowsAzureStorageQueues.Config
{
    using System;

    public sealed class QueueAtAccount : IEquatable<QueueAtAccount>
    {
        public const string DefaultStorageAccountName = "";
        public const string Separator = "@";

        public string QueueName { get; }
        public string StorageAccount { get; }

        public QueueAtAccount(string queueName, string storageAccount)
        {
            if (string.IsNullOrWhiteSpace(queueName))
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

        public static QueueAtAccount Parse(string value)
        {
            QueueAtAccount q;
            if (TryParse(value, out q) == false)
            {
                throw new ArgumentException("Value cannot be parsed", nameof(value));
            }

            return q;
        }

        public static bool TryParse(string inputQueue, out QueueAtAccount queue)
        {
            var index = inputQueue.IndexOf(Separator, StringComparison.Ordinal);
            if (index < 0)
            {
                queue = new QueueAtAccount(inputQueue, DefaultStorageAccountName);
                return true;
            }
            
            queue = new QueueAtAccount(inputQueue.Substring(0, index), inputQueue.Substring(index+1));
            return true;
        }

        public bool Equals(QueueAtAccount other)
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
            return obj is QueueAtAccount && Equals((QueueAtAccount) obj);
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
            return $"{QueueName}@{StorageAccount}";
        }
    }
}