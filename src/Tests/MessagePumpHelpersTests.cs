namespace NServiceBus.AzureStorageQueues.Tests
{
    using System.Collections.Generic;
    using System.Linq;
    using NUnit.Framework;
    using Transport.AzureStorageQueues;

    [TestFixture]
    class MessagePumpHelpersTests
    {
        public static IEnumerable<object[]> Configuration
        {
            get
            {
                // concurrency based
                yield return new object[]
                {
                    null, null, 1, new List<ReceiverConfiguration>
                    {
                        new ReceiverConfiguration(batchSize: 2)
                    }
                };
                yield return new object[]
                {
                    null, null, 32, new List<ReceiverConfiguration>
                    {
                        new ReceiverConfiguration(batchSize: 32)
                    }
                };
                yield return new object[]
                {
                    null, null, 33, new List<ReceiverConfiguration>
                    {
                        new ReceiverConfiguration(batchSize: 32),
                        new ReceiverConfiguration(batchSize: 8)
                    }
                };

                yield return new object[]
                {
                    null, null, 105, new List<ReceiverConfiguration>
                    {
                        new ReceiverConfiguration(batchSize: 32),
                        new ReceiverConfiguration(batchSize: 32),
                        new ReceiverConfiguration(batchSize: 32),
                        new ReceiverConfiguration(batchSize: 30)
                    }
                };

                // explicit batch size
                yield return new object[]
                {
                    5, null, 1, new List<ReceiverConfiguration>
                    {
                        new ReceiverConfiguration(batchSize: 5)
                    }
                };
                yield return new object[]
                {
                    5, null, 13, new List<ReceiverConfiguration>
                    {
                        new ReceiverConfiguration(batchSize: 5),
                        new ReceiverConfiguration(batchSize: 5),
                        new ReceiverConfiguration(batchSize: 5),
                        new ReceiverConfiguration(batchSize: 5),
                    }
                };
                yield return new object[]
                {
                    5, null, 6, new List<ReceiverConfiguration>
                    {
                        new ReceiverConfiguration(batchSize: 5),
                        new ReceiverConfiguration(batchSize: 5)
                    }
                };

                // explicit degree of parallelism
                yield return new object[]
                {
                    null, 2, 1, new List<ReceiverConfiguration>
                    {
                        new ReceiverConfiguration(batchSize: 1),
                        new ReceiverConfiguration(batchSize: 1)
                    }
                };

                yield return new object[]
                {
                    null, 2, 15, new List<ReceiverConfiguration>
                    {
                        new ReceiverConfiguration(batchSize: 9),
                        new ReceiverConfiguration(batchSize: 9)
                    }
                };

                yield return new object[]
                {
                    null, 2, 32, new List<ReceiverConfiguration>
                    {
                        new ReceiverConfiguration(batchSize: 19),
                        new ReceiverConfiguration(batchSize: 19)
                    }
                };

                yield return new object[]
                {
                    null, 2, 62, new List<ReceiverConfiguration>
                    {
                        new ReceiverConfiguration(batchSize: 32),
                        new ReceiverConfiguration(batchSize: 32)
                    }
                };

                yield return new object[]
                {
                    null, 2, 63, new List<ReceiverConfiguration>
                    {
                        new ReceiverConfiguration(batchSize: 32),
                        new ReceiverConfiguration(batchSize: 32)
                    }
                };

                yield return new object[]
                {
                    null, 2, 65, new List<ReceiverConfiguration>
                    {
                        new ReceiverConfiguration(batchSize: 32),
                        new ReceiverConfiguration(batchSize: 32)
                    }
                };

                // control both
                yield return new object[]
                {
                    16, 2, 65, new List<ReceiverConfiguration>
                    {
                        new ReceiverConfiguration(batchSize: 16),
                        new ReceiverConfiguration(batchSize: 16)
                    }
                };

                yield return new object[]
                {
                    16, 1, 65, new List<ReceiverConfiguration>
                    {
                        new ReceiverConfiguration(batchSize: 16)
                    }
                };
            }
        }

        [Test]
        [TestCaseSource("Configuration")]
        public void Should_determine_receiver_configuration(int? receiveBatchSize, int? degreeOfReceiverParallelism, int maximumConcurrency, IEnumerable<ReceiverConfiguration> expected)
        {
            var result = MessagePumpHelpers.DetermineReceiverConfiguration(receiveBatchSize, degreeOfReceiverParallelism, maximumConcurrency);

            var expectedString = string.Join(";", expected.Select(x => x.BatchSize));
            var resultString = string.Join(";", result.Select(x => x.BatchSize));

            CollectionAssert.AreEquivalent(expected, result, $"Expected: {expectedString}\r\nResult: {resultString}");
        }
    }
}