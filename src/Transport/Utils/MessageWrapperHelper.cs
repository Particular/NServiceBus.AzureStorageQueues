namespace NServiceBus.Transport.AzureStorageQueues
{
    using System;
    using System.IO;
    using NServiceBus.Azure.Transports.WindowsAzureStorageQueues;

    static class MessageWrapperHelper
    {
        public static string ConvertToBase64String(MessageWrapper wrapper, MessageWrapperSerializer serializer)
        {
            string base64String;

            using (var stream = new MemoryStream())
            {
                serializer.Serialize(wrapper, stream);

                var bytes = stream.ToArray();
                base64String = Convert.ToBase64String(bytes);
            }
            return base64String;
        }
    }
}
