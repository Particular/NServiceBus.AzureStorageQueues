namespace NServiceBus.AzureStorageQueues
{
    using System;

    struct ConnectionString
    {
        public bool Equals(ConnectionString other)
        {
            return string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }
            return obj is ConnectionString s && Equals(s);
        }

        public override int GetHashCode()
        {
            return StringComparer.OrdinalIgnoreCase.GetHashCode(Value);
        }

        public static bool operator ==(ConnectionString left, ConnectionString right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ConnectionString left, ConnectionString right)
        {
            return !left.Equals(right);
        }

        public ConnectionString(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Connection string cannot be null nor empty.", nameof(value));
            }

            Value = value;
        }

        public override string ToString()
        {
            return Value;
        }

        public readonly string Value;
    }
}