namespace NServiceBus.Azure.Transports.WindowsAzureStorageQueues
{
    using System.Diagnostics;

    static class IsHostedIn
    {
        public static string HostProcessName = "NServiceBus.Hosting.Azure.HostProcess";

        public static bool ChildHostProcess()
        {
            var currentProcess = Process.GetCurrentProcess();
            return currentProcess.ProcessName == HostProcessName;
        }
    }
}