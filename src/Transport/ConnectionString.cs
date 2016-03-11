﻿namespace NServiceBus.Azure.Transports.WindowsAzureStorageQueues
{
    using System;

    public sealed class ConnectionString : IEquatable<ConnectionString>
    {
        public readonly string Value;

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

        public bool Equals(ConnectionString other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }
            if (ReferenceEquals(this, other))
            {
                return true;
            }
            return string.Equals(Value, other.Value);
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
            if (obj.GetType() != GetType())
            {
                return false;
            }
            return Equals((ConnectionString)obj);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }
    }
}