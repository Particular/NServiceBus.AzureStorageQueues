namespace NServiceBus.Azure.WindowsAzureServiceBus.Tests.API
{
    using System.IO;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using ApprovalTests.Reporters;
    using NUnit.Framework;
    using PublicApiGenerator;

    [TestFixture]
    public class APIApprovals
    {
        [Test]
        [MethodImpl(MethodImplOptions.NoInlining)]
        [UseReporter(typeof(DiffReporter), typeof(AllFailingTestsClipboardReporter))]
        public void ApproveAzureStorageQueueTransport()
        {
            var combine = Path.Combine(TestContext.CurrentContext.TestDirectory, "NServiceBus.Azure.Transports.WindowsAzureStorageQueues.dll");
            var assembly = Assembly.LoadFile(combine);
            ApiGenerator.GeneratePublicApi(assembly);
        }
    }
}