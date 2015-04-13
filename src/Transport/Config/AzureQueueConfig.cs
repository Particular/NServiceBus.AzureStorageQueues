namespace NServiceBus.Config
{
    using System.Configuration;
    using Azure.Transports.WindowsAzureStorageQueues;
    using NServiceBus.Logging;

    public class AzureQueueConfig : ConfigurationSection
    {
        ILog logger = LogManager.GetLogger<AzureQueueConfig>();

        [ObsoleteEx(RemoveInVersion = "8", TreatAsErrorFromVersion = "7", Replacement = "configuration.EndpointName(name)")]        
        [ConfigurationProperty("QueueName", IsRequired = false, DefaultValue = null)]
        public string QueueName
        {
            get
            {
                return (string)this["QueueName"];
            }
            set
            {
                logger.Warn("AzureQueueConfig.QueueName is deprecated and will be removed in version 8.0. Use `configuration.EndpointName(name)` instead to define endpoint and input queue names.");
                this["QueueName"] = value;
            }
        }

        [ConfigurationProperty("ConnectionString", IsRequired = false, DefaultValue = AzureMessageQueueReceiver.DefaultConnectionString)]
        public string ConnectionString
        {
            get
            {
                return (string)this["ConnectionString"];
            }
            set
            {
                this["ConnectionString"] = value;
            }
        }

        [ConfigurationProperty("PeekInterval", IsRequired = false, DefaultValue = AzureMessageQueueReceiver.DefaultPeekInterval)]
        public int PeekInterval
        {
            get
            {
                return (int)this["PeekInterval"];
            }
            set
            {
                this["PeekInterval"] = value;
            }
        }

        [ConfigurationProperty("MaximumWaitTimeWhenIdle", IsRequired = false, DefaultValue = AzureMessageQueueReceiver.DefaultMaximumWaitTimeWhenIdle)]
        public int MaximumWaitTimeWhenIdle
        {
            get
            {
                return (int)this["MaximumWaitTimeWhenIdle"];
            }
            set
            {
                this["MaximumWaitTimeWhenIdle"] = value;
            }
        }

        [ConfigurationProperty("PurgeOnStartup", IsRequired = false, DefaultValue = AzureMessageQueueReceiver.DefaultPurgeOnStartup)]
        public bool PurgeOnStartup
        {
            get
            {
                return (bool)this["PurgeOnStartup"];
            }
            set
            {
                this["PurgeOnStartup"] = value;
            }
        }

        [ConfigurationProperty("MessageInvisibleTime", IsRequired = false, DefaultValue = AzureMessageQueueReceiver.DefaultMessageInvisibleTime)]
        public int MessageInvisibleTime
        {
            get
            {
                return (int)this["MessageInvisibleTime"];
            }
            set
            {
                this["MessageInvisibleTime"] = value;
            }
        }

        [ConfigurationProperty("BatchSize", IsRequired = false, DefaultValue = AzureMessageQueueReceiver.DefaultBatchSize)]
        public int BatchSize
        {
            get
            {
                return (int)this["BatchSize"];
            }
            set
            {
                this["BatchSize"] = value;
            }
        }

        [ObsoleteEx(RemoveInVersion = "8", TreatAsErrorFromVersion = "7", Replacement = "configuration.ScaleOut().UniqueQueuePerEndpointInstance()")]        
        [ConfigurationProperty("QueuePerInstance", IsRequired = false, DefaultValue = AzureMessageQueueReceiver.DefaultQueuePerInstance)]
        public bool QueuePerInstance
        {
            get
            {
                return (bool)this["QueuePerInstance"];
            }
            set
            {
                logger.Warn("AzureQueueConfig.QueuePerInstance is deprecated and will be removed in version 8.0. Use `configuration.ScaleOut().UniqueQueuePerEndpointInstance()` instead.");
                this["QueuePerInstance"] = value;
            }
        }
   }
}