namespace NServiceBus.Transport.AzureStorageQueues.Tests
{
    using System.Collections.Generic;
    using System.Linq;
    using NUnit.Framework;
    using AzureStorageQueues;

    [TestFixture]
    class MessagePumpHelpersTests
    {
        static IEnumerable<object[]> ConcurrencyBased
        {
            get
            {
                // concurrency based
                yield return new object[]
                {
                    null, null, 1, new List<ReceiverConfiguration>
                    {
                        new ReceiverConfiguration(batchSize: 1)
                    }
                };
                yield return new object[]
                {
                    null, null, 32, new List<ReceiverConfiguration>
                    {
                        new ReceiverConfiguration(batchSize: 32),
                    }
                };
                yield return new object[]
                {
                    null, null, 33, new List<ReceiverConfiguration>
                    {
                        new ReceiverConfiguration(batchSize: 32),
                        new ReceiverConfiguration(batchSize: 1)
                    }
                };

                yield return new object[]
                {
                    null, null, 105, new List<ReceiverConfiguration>
                    {
                        new ReceiverConfiguration(batchSize: 32),
                        new ReceiverConfiguration(batchSize: 32),
                        new ReceiverConfiguration(batchSize: 32),
                        new ReceiverConfiguration(batchSize: 9)
                    }
                };
            }
        }

        static IEnumerable<object[]> FixedBatchSize
        {
            get
            {
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
                        new ReceiverConfiguration(batchSize: 5)
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

            }
        }

        static IEnumerable<object[]> FixedDegreeOfParallelism
        {
            get
            {
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
                        new ReceiverConfiguration(batchSize: 8),
                        new ReceiverConfiguration(batchSize: 8)
                    }
                };

                yield return new object[]
                {
                    null, 2, 32, new List<ReceiverConfiguration>
                    {
                        new ReceiverConfiguration(batchSize: 16),
                        new ReceiverConfiguration(batchSize: 16)
                    }
                };

                yield return new object[]
                {
                    null, 2, 62, new List<ReceiverConfiguration>
                    {
                        new ReceiverConfiguration(batchSize: 31),
                        new ReceiverConfiguration(batchSize: 31)
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
            }
        }

        static IEnumerable<object[]> FixedDegreeOfParallelismAndBatchSize
        {
            get
            {
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
        [TestCaseSource(nameof(ConcurrencyBased))]
        public void Should_calculate_degree_of_parallelism_and_batch_sized_based_on_concurrency(int? receiveBatchSize, int? degreeOfReceiverParallelism, int maximumConcurrency, IEnumerable<ReceiverConfiguration> expected)
        {
            var result = MessagePumpHelpers.DetermineReceiverConfiguration(receiveBatchSize, degreeOfReceiverParallelism, maximumConcurrency);

            var expectedString = string.Join(";", expected.Select(x => x.BatchSize));
            var resultString = string.Join(";", result.Select(x => x.BatchSize));

            CollectionAssert.AreEquivalent(expected, result, $"Expected: {expectedString}\r\nResult: {resultString}");
        }

        [Test]
        [TestCaseSource(nameof(FixedBatchSize))]
        public void Should_calculate_degree_of_parallelism_based_on_fixed_batch_size(int? receiveBatchSize, int? degreeOfReceiverParallelism, int maximumConcurrency, IEnumerable<ReceiverConfiguration> expected)
        {
            var result = MessagePumpHelpers.DetermineReceiverConfiguration(receiveBatchSize, degreeOfReceiverParallelism, maximumConcurrency);

            var expectedString = string.Join(";", expected.Select(x => x.BatchSize));
            var resultString = string.Join(";", result.Select(x => x.BatchSize));

            CollectionAssert.AreEquivalent(expected, result, $"Expected: {expectedString}\r\nResult: {resultString}");
        }

        [Test]
        [TestCaseSource(nameof(FixedDegreeOfParallelism))]
        public void Should_calculate_batch_size_based_on_fixed_degree_of_parallelism(int? receiveBatchSize, int? degreeOfReceiverParallelism, int maximumConcurrency, IEnumerable<ReceiverConfiguration> expected)
        {
            var result = MessagePumpHelpers.DetermineReceiverConfiguration(receiveBatchSize, degreeOfReceiverParallelism, maximumConcurrency);

            var expectedString = string.Join(";", expected.Select(x => x.BatchSize));
            var resultString = string.Join(";", result.Select(x => x.BatchSize));

            CollectionAssert.AreEquivalent(expected, result, $"Expected: {expectedString}\r\nResult: {resultString}");
        }

        [Test]
        [TestCaseSource(nameof(FixedDegreeOfParallelismAndBatchSize))]
        public void Should_use_fixed_batch_size_and_degree_if_provided(int? receiveBatchSize, int? degreeOfReceiverParallelism, int maximumConcurrency, IEnumerable<ReceiverConfiguration> expected)
        {
            var result = MessagePumpHelpers.DetermineReceiverConfiguration(receiveBatchSize, degreeOfReceiverParallelism, maximumConcurrency);

            var expectedString = string.Join(";", expected.Select(x => x.BatchSize));
            var resultString = string.Join(";", result.Select(x => x.BatchSize));

            CollectionAssert.AreEquivalent(expected, result, $"Expected: {expectedString}\r\nResult: {resultString}");
        }
    }
}