namespace NServiceBus
{
    using System;
    using System.Collections.Generic;
    using AzureStorageQueues;

    /// <summary>
    /// 
    /// </summary>
    public class AccountInfo
    {
        /// <summary>
        /// 
        /// </summary>
        public AccountInfo(string alias, string connectionString)
        {
            Guard.AgainstNullAndEmpty(nameof(connectionString), connectionString);
            
            Alias = alias;
            Connection = new ConnectionString(connectionString);
            RegisteredEndpoints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
        
        /// <summary>
        /// 
        /// </summary>
        public string Alias { get; }
        
        /// <summary>
        /// 
        /// </summary>
        public string ConnectionString => Connection.Value;
        
        /// <summary>
        /// 
        /// </summary>
        public HashSet<string> RegisteredEndpoints { get; }
        
        internal ConnectionString Connection { get; }
    }
}