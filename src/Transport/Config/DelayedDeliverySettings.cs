namespace NServiceBus
{
    using System;
    using System.Text.RegularExpressions;

    /// <summary>Configures native delayed delivery.</summary>
    public class DelayedDeliverySettings
    {
        internal string TableName;
        internal bool TimeoutManagerDisabled;

        internal bool TableNameWasNotOverridden => string.IsNullOrEmpty(TableName);

        /// <summary>Override the default table name used for storing delayed messages.</summary>
        /// <param name="delayedMessagesTableName">New table name.</param>
        public void UseTableName(string delayedMessagesTableName)
        {
            Guard.AgainstNullAndEmpty(nameof(delayedMessagesTableName), delayedMessagesTableName);

            const string tableNameRegex = "^[A-Za-z][A-Za-z0-9]{2,62}$";
            if (new Regex(tableNameRegex).IsMatch(delayedMessagesTableName) == false)
            {
                throw new ArgumentException($"{nameof(delayedMessagesTableName)} must match the following regular expression '{tableNameRegex}'");
            }
            
            TableName = delayedMessagesTableName.ToLower();
        }

        /// <summary>Disables the Timeout Manager for the endpoint. Before disabling ensure there all timeouts in the timeout store have been processed or migrated.</summary>
        public void DisableTimeoutManager()
        {
            TimeoutManagerDisabled = true;
        }
    }
}
