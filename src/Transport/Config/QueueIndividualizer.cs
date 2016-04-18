namespace NServiceBus.Azure.Transports.WindowsAzureStorageQueues
{
    using System.Globalization;
    using Config;
    using Support;

    public class QueueIndividualizer
    {
        public static string Discriminator
        {
            get
            {
                if (SafeRoleEnvironment.IsAvailable)
                {
                    var index = ParseIndexFrom(SafeRoleEnvironment.CurrentRoleInstanceId);

                    return "-" + index.ToString(CultureInfo.InvariantCulture);
                }
                return "-" + RuntimeEnvironment.MachineName;
            }
        }

        public static string Individualize(string queueName)
        {
            var individualQueueName = queueName;
            var queue = QueueAddress.Parse(queueName);
            var currentQueue = queue.QueueName;
            var account = queue.StorageAccount;

            if (SafeRoleEnvironment.IsAvailable)
            {
                var index = ParseIndexFrom(SafeRoleEnvironment.CurrentRoleInstanceId);

                var indexAsString = index.ToString(CultureInfo.InvariantCulture);
                if (!currentQueue.EndsWith("-" + indexAsString)) //individualize can be applied multiple times
                {
                    individualQueueName = currentQueue
                                          + (index > 0 ? "-" : "")
                                          + (index > 0 ? indexAsString : "");

                    if (queueName.Contains(QueueAddress.Separator))
                    {
                        individualQueueName += QueueAddress.Separator + account;
                    }
                }
            }
            else
            {
                if (!currentQueue.EndsWith("-" + RuntimeEnvironment.MachineName)) //individualize can be applied multiple times
                {
                    individualQueueName = $"{currentQueue}-{RuntimeEnvironment.MachineName}";

                    if (queueName.Contains(QueueAddress.Separator))
                    {
                        individualQueueName += QueueAddress.Separator + account;
                    }
                }
            }

            return individualQueueName;
        }

        public static int ParseIndexFrom(string id)
        {
            var idArray = id.Split('.');
            int index;
            if (!int.TryParse((idArray[idArray.Length - 1]), out index))
            {
                idArray = id.Split('_');
                index = int.Parse((idArray[idArray.Length - 1]));
            }
            return index;
        }
    }
}