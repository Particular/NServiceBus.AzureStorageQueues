namespace NServiceBus.Azure.Transports.WindowsAzureStorageQueues
{
    using System;
    using System.Globalization;
    using NServiceBus.Azure.Transports.WindowsAzureStorageQueues.Config;
    using Support;

    public class QueueIndividualizer
    {
        public static string Individualize(string queueName)
        {
            var individualQueueName = queueName;
            var queue = QueueAtAccount.Parse(queueName);
            var currentQueue = queue.QueueName;
            var account = queue.StorageAccount;

            if (SafeRoleEnvironment.IsAvailable)
            {
                var index = ParseIndexFrom(SafeRoleEnvironment.CurrentRoleInstanceId);

                if (!currentQueue.EndsWith("-" + index.ToString(CultureInfo.InvariantCulture))) //individualize can be applied multiple times
                {
                    individualQueueName = currentQueue
                                          + (index > 0 ? "-" : "")
                                          + (index > 0 ? index.ToString(CultureInfo.InvariantCulture) : "");

                    if (queueName.Contains(QueueAtAccount.Separator))
                        individualQueueName += QueueAtAccount.Separator + account;
                }
            }
            else
            {
                if (!currentQueue.EndsWith("-" + RuntimeEnvironment.MachineName)) //individualize can be applied multiple times
                {
                    individualQueueName = currentQueue + "-" + RuntimeEnvironment.MachineName;

                    if (queueName.Contains(QueueAtAccount.Separator))
                        individualQueueName += QueueAtAccount.Separator + account;
                }
            }

            return individualQueueName;
        }

        public static string Discriminator {
            get
            {
                if (SafeRoleEnvironment.IsAvailable)
                {
                    var index = ParseIndexFrom(SafeRoleEnvironment.CurrentRoleInstanceId);

                    return "-" + index.ToString(CultureInfo.InvariantCulture);
                }
                else
                {
                    return "-" + RuntimeEnvironment.MachineName;
                }
            }
        }

        public static int ParseIndexFrom(string id)
        {
            var idArray = id.Split('.');
            int index;
            if (!Int32.TryParse((idArray[idArray.Length - 1]), out index))
            {
                idArray = id.Split('_');
                index = Int32.Parse((idArray[idArray.Length - 1]));
            }
            return index;
        }
    }
}