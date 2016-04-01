using System.Net;
using NUnit.Framework;

[SetUpFixture]
public class NamespaceSetup
{
    [SetUp]
    public void SetUp()
    {
        ServicePointManager.DefaultConnectionLimit = 64;
    }
}