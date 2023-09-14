// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NServiceBus.Transport.AzureStorageQueues.Tests.Utils
{
    using System;
    using System.Threading.Tasks;
    using AzureStorageQueues.Utils;
    using NUnit.Framework;

    public class ValueStopwatchTests
    {
        [Test]
        public void IsActiveIsFalseForDefaultValueStopwatch()
        {
            Assert.False(default(ValueStopwatch).IsActive);
        }

        [Test]
        public void IsActiveIsTrueWhenValueStopwatchStartedWithStartNew()
        {
            Assert.True(ValueStopwatch.StartNew().IsActive);
        }

        [Test]
        public void GetElapsedTimeThrowsIfValueStopwatchIsDefaultValue()
        {
            var stopwatch = default(ValueStopwatch);
            Assert.Throws<InvalidOperationException>(() => stopwatch.GetElapsedTime());
        }

        [Test]
        public async Task GetElapsedTimeReturnsTimeElapsedSinceStart()
        {
            var stopwatch = ValueStopwatch.StartNew();
            await Task.Delay(200);
            Assert.True(stopwatch.GetElapsedTime().TotalMilliseconds > 0);
        }
    }
}