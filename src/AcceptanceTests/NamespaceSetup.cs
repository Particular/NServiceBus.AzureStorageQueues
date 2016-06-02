using System.Net;
using NUnit.Framework;

[SetUpFixture]
public class NamespaceSetup
{
    [OneTimeSetUp]
    public void SetUp()
    {
        ServicePointManager.DefaultConnectionLimit = 64;
    }
}