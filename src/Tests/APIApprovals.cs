using NServiceBus;
using NUnit.Framework;
using Particular.Approvals;
using PublicApiGenerator;

[TestFixture]
public class APIApprovals
{
    [Test]
    public void ApproveAzureStorageQueueTransport()
    {
        var publicApi = typeof(AzureStorageQueueTransport).Assembly.GeneratePublicApi(new ApiGeneratorOptions
        {
            ExcludeAttributes = new[] { "System.Runtime.Versioning.TargetFrameworkAttribute" }
        });
        Approver.Verify(publicApi);
    }
}