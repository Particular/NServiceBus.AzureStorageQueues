namespace NServiceBus.Azure.QuickTests
{
    using AzureStorageQueues;
    using NUnit.Framework;

    public class ShortenerTests
    {
        [Test]
        public void Md5()
        {
            Assert.AreEqual("2c2c0c7b-98bc-5501-d71b-a3be4d174f56", Shortener.Md5(TestName));
        }

        [Test]
        public void Sha1()
        {
            Assert.AreEqual("e5bc909de4c00a5266878bdea494203b9936328c", Shortener.Sha1(TestName));
        }

        const string TestName = "test me";
    }
}