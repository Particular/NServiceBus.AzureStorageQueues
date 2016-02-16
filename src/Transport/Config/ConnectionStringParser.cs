namespace NServiceBus.Azure.Transports.WindowsAzureStorageQueues
{
    using System;

    public static class ConnectionStringParser
    {
        public static bool TryParseNamespaceFrom(string inputQueue, out string @namespace)
        {
            if (inputQueue.Contains("@") == false)
            {
                @namespace = null;
                return false;
            }
            @namespace = inputQueue.Substring(inputQueue.IndexOf("@", StringComparison.Ordinal) + 1);
            return true;
        }

        public static string ParseNamespaceFrom(string inputQueue)
        {
            string @namespace;
            return TryParseNamespaceFrom(inputQueue, out @namespace) ? @namespace : string.Empty;
        }

        public static string ParseQueueNameFrom(string inputQueue)
        {
            return inputQueue.Contains("@") ? inputQueue.Substring(0, inputQueue.IndexOf("@", StringComparison.Ordinal)) : inputQueue;
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