namespace NServiceBus.Azure.QuickTests.Transports.AzureStorage
{
    using NServiceBus.AzureStorageQueues;
    using NServiceBus.Settings;
    using NUnit.Framework;

    [TestFixture]
    [Category("Azure")]
    public class When_naming_queues_on_azure_storage
    {
        [TestCase("Test1234Queue", "test1234queue", false)]
        [TestCase("Test.Queue", "test-queue", false)]
        [TestCase("TestQueueTestQueueTestQueueTestQueueTestQueueTestQueueTestQueue", "testqueuetestqueuetestqueuetestqueuetestqueuetestqueuetestqueue", false)]
        [TestCase("Test1234Queue", "test1234queue", false)]
        public void Should_fix_queue_name_when_upper_case_letters_are_used_dots_or_longer_than_63_characters(string queueName, string expected, bool useSha1)
        {
            var generator = BuildGenerator(useSha1);
            var name = generator.GetQueueName(queueName);
            Assert.AreEqual(expected, name);
        }

        [TestCase("Test_Queue", "test-queue", false)]
        [TestCase("-TestQueue", "-testqueue", false)]
        [TestCase("TestQueue-", "testqueue-", false)]
        [TestCase("TQ", "tq", false)]
        public void Should_fix_queue_name_when_invalid(string queueName, string expected, bool useSha1)
        {
            var generator = BuildGenerator(useSha1);
            var name = generator.GetQueueName(queueName);
            Assert.AreEqual(expected, name);
        }

        static QueueAddressGenerator BuildGenerator(bool useSha1ForShortening)
        {
            var holder = new SettingsHolder();
            holder.Set("UseSha1ForShortening", useSha1ForShortening);
            return new QueueAddressGenerator(holder);
        }
    }
}