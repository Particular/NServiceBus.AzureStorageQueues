namespace NServiceBus
{
    using System;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Configures native delayed delivery.
    /// </summary>
    public class DelayedDeliverySettings
    {
        internal string Name;
        internal bool TimeoutManagerDisabled;

        /// <summary>
        /// Sets the table name for the table storing delayed messages.
        /// </summary>
        internal void TableName(string timeoutTableName)
        {
            Guard.AgainstNullAndEmpty(nameof(timeoutTableName), timeoutTableName);

            const string tableNameRegex = "^[A-Za-z][A-Za-z0-9]{2,62}$";
            if (new Regex(tableNameRegex).IsMatch(timeoutTableName) == false)
            {
                throw new ArgumentException($"{nameof(timeoutTableName)} must match the following regular expression '{tableNameRegex}'");
            }
            
            Name = timeoutTableName.ToLower();
        }

        /// <summary>
        /// Disables the Timeout Manager for the endpoint. Before disabling ensure there all timeouts in the timeout store have been processed or migrated.
        /// </summary>
        public void DisableTimeoutManager()
        {
            TimeoutManagerDisabled = true;
        }
    }
}
