namespace NServiceBus.Azure.Transports.WindowsAzureStorageQueues
{
    // The message:
    // This class served only internal purposes without providing any extensibility point and as such was removed from the public API. For more information, refer to the documentation.
    // is used in Upgrade guide v6 to v7 for ASQ, when changing this message please 
    // make sure to change the documentation as well.
    [ObsoleteEx(Message = "This class served only internal purposes without providing any extensibility point and as such was removed from the public API. For more information, refer to the documentation.", 
        RemoveInVersion = "8.0", TreatAsErrorFromVersion = "7.0")]
    public class AzureMessageQueueUtils
    {
    }

    [ObsoleteEx(Message = "This class served only internal purposes without providing any extensibility point and as such was removed from the public API. For more information, refer to the documentation.", 
        RemoveInVersion = "8.0", TreatAsErrorFromVersion = "7.0")]
    public class DeterministicGuidBuilder
    {
    }

    [ObsoleteEx(Message = "This class served only internal purposes without providing any extensibility point and as such was removed from the public API. For more information, refer to the documentation.", 
        RemoveInVersion = "8.0", TreatAsErrorFromVersion = "7.0")]
    public class PollingDequeueStrategy
    {
    }

    [ObsoleteEx(Message = "This class served only internal purposes without providing any extensibility point and as such was removed from the public API. For more information, refer to the documentation.", 
        RemoveInVersion = "8.0", TreatAsErrorFromVersion = "7.0")]
    public class ReceiveResourceManager
    {
    }

    [ObsoleteEx(Message = "This class served only internal purposes without providing any extensibility point and as such was removed from the public API. For more information, refer to the documentation.", 
        RemoveInVersion = "8.0", TreatAsErrorFromVersion = "7.0")]
    public class SendResourceManager
    {
    }

    [ObsoleteEx(Message = "This class served only internal purposes without providing any extensibility point and as such was removed from the public API. For more information, refer to the documentation.", 
        RemoveInVersion = "8.0", TreatAsErrorFromVersion = "7.0")]
    public class ConnectionStringParser
    {
    }

    [ObsoleteEx(Message = "This class served only internal purposes without providing any extensibility point and as such was removed from the public API. For more information, refer to the documentation.", 
        RemoveInVersion = "8.0", TreatAsErrorFromVersion = "7.0")]
    public class DeterminesBestConnectionStringForStorageQueues
    {
    }

    [ObsoleteEx(Message = "This class served only internal purposes without providing any extensibility point and as such was removed from the public API. For more information, refer to the documentation.", 
        RemoveInVersion = "8.0", TreatAsErrorFromVersion = "7.0")]
    public class QueueAutoCreation
    {
    }

    [ObsoleteEx(Message = "This class served only internal purposes without providing any extensibility point and as such was removed from the public API. For more information, refer to the documentation.", 
        RemoveInVersion = "8.0", TreatAsErrorFromVersion = "7.0")]
    public class AzureQueueNamingConvention
    {
    }

    [ObsoleteEx(Message = "This class served only internal purposes without providing any extensibility point and as such was removed from the public API. For more information, refer to the documentation.", 
        RemoveInVersion = "8.0", TreatAsErrorFromVersion = "7.0")]
    public class IsHostedIn
    {
    }

    [ObsoleteEx(Message = "This exception was used only within the library and was not thrown outside. As such it was marked as internal.", 
        RemoveInVersion = "8.0", TreatAsErrorFromVersion = "7.0")]
    public class EnvelopeDeserializationFailed
    {
    }

    [ObsoleteEx(Message = "This class served only internal purposes without providing any extensibility point and as such was removed from the public API. For more information, refer to the documentation.", 
        RemoveInVersion = "8.0", TreatAsErrorFromVersion = "7.0")]
    public class AzureMessageQueueCreator
    {
    }

    [ObsoleteEx(Message = "This class served only internal purposes without providing any extensibility point and as such was removed from the public API. For more information, refer to the documentation.", 
        RemoveInVersion = "8.0", TreatAsErrorFromVersion = "7.0")]
    public class AzureMessageQueueReceiver
    {
    }

    [ObsoleteEx(Message = "This class served only internal purposes without providing any extensibility point and as such was removed from the public API. For more information, refer to the documentation.", 
        RemoveInVersion = "8.0", TreatAsErrorFromVersion = "7.0")]
    public class AzureMessageQueueSender
    {
    }

    [ObsoleteEx(Message = "This exception was used only within the library and was not thrown outside. As such it was marked as internal.", 
        RemoveInVersion = "8.0", TreatAsErrorFromVersion = "7.0")]
    public class SafeRoleEnvironment
    {
    }

    [ObsoleteEx(Message = "This class served only internal purposes without providing any extensibility point and as such was removed from the public API. For more information, refer to the documentation.", 
        RemoveInVersion = "8.0", TreatAsErrorFromVersion = "7.0")]
    public class CreateQueueClients
    {
    }

    [ObsoleteEx(Message = "This interface served only internal implementations and as such was removed from the public API. For more information, refer to the documentation.", 
        RemoveInVersion = "8.0", TreatAsErrorFromVersion = "7.0")]
    public class ICreateQueueClients
    {
    }

    [ObsoleteEx(Message = "This exception provided no value to the users, Exception is thrown in that place with a message that role environment variable was not found.", 
        RemoveInVersion = "8.0", TreatAsErrorFromVersion = "7.0")]
    public class RoleEnvironmentUnavailableException
    {
    }

    //[ObsoleteEx(Message = "This class contained methods that were not supposed to be public.", RemoveInVersion = "8.0", TreatAsErrorFromVersion = "7.0")]
    //public class UnableToDispatchException
    //{
    //}
}

namespace NServiceBus.Config
{
    [ObsoleteEx(Message = "This class served only internal purposes without providing any extensibility point and as such was removed from the public API. For more information, refer to the documentation.", 
        RemoveInVersion = "8.0", TreatAsErrorFromVersion = "7.0")]
    public class AzureQueueConfig
    {
    }


}

namespace NServiceBus.Features
{
    [ObsoleteEx(Message = "This class served only internal purposes without providing any extensibility point and as such was removed from the public API. For more information, refer to the documentation.", 
        RemoveInVersion = "8.0", TreatAsErrorFromVersion = "7.0")]
    public class AzureStorageQueueTransport
    {
    }
}

namespace NServiceBus
{
    [ObsoleteEx(Message = "This class was replaced by extension methods on endpointConfiguration.UseTransport<AzureStorageQueue>()", 
        RemoveInVersion = "8.0", TreatAsErrorFromVersion = "7.0")]
    public static class ConfigureAzureMessageQueue
    {
    }
}