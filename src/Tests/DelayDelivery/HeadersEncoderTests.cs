namespace NServiceBus.Azure.Transports.WindowsAzureStorageQueues.AcceptanceTests.DelayDelivery
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using WindowsAzureStorageQueues.DelayDelivery;
    using NUnit.Framework;
    using NUnit.Framework.Interfaces;

    public class HeadersEncoderTests
    {
        [TestCaseSource(nameof(HeaderValues))]
        public void Test(Dictionary<string, string> values)
        {
            var bytes = HeadersEncoder.Serialize(values);
            var actual = HeadersEncoder.Deserialize(bytes);

            CollectionAssert.AreEqual(values.OrderBy(kvp => kvp.Key), actual.OrderBy(kvp => kvp.Key), new KeyValuePairComparer());
        }

        static IEnumerable<ITestCaseData> HeaderValues()
        {
            yield return new TestCaseData(new Dictionary<string, string>()).SetName("Empty");
            yield return new TestCaseData(new Dictionary<string, string> { { "test", "" } }).SetName("Empty value");
            yield return new TestCaseData(new Dictionary<string, string> { { "k1", "v1" }, {"k2","v2"} }).SetName("Two values");

            var key = new string('a',1024);
            var value = new string('b',1024);

            yield return new TestCaseData(new Dictionary<string, string>{{key, value}}).SetName("Very long keys and values");
        }

        class KeyValuePairComparer : IComparer
        {
            public int Compare(object x, object y)
            {
                var kvp1 = (KeyValuePair<string, string>)x;
                var kvp2 = (KeyValuePair<string, string>)y;

                var comparison = string.Compare(kvp1.Key, kvp2.Key, StringComparison.Ordinal);
                if (comparison != 0)
                {
                    return comparison;
                }

                return string.Compare(kvp1.Value, kvp2.Value, StringComparison.Ordinal);
            }
        }
    }
}