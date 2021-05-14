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
        var publicApi = ApiGenerator.GeneratePublicApi(typeof(AzureStorageQueueTransport).Assembly, excludeAttributes: new[] { "System.Runtime.Versioning.TargetFrameworkAttribute", "System.Reflection.AssemblyMetadataAttribute" });
        Approver.Verify(publicApi, s => s.Replace(@"\\r\\n", @"\\n"));
    }
}