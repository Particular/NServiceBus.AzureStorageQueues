// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NServiceBus.Transport.AzureStorageQueues.Utils
{
    using System;
    using System.Diagnostics;

    // https://github.com/dotnet/aspnetcore/blob/master/src/Shared/ValueStopwatch/ValueStopwatch.cs
    struct ValueStopwatch
    {
        static readonly double TimestampToTicks = TimeSpan.TicksPerSecond / (double)Stopwatch.Frequency;

        long startTimestamp;

        public bool IsActive => startTimestamp != 0;

        ValueStopwatch(long startTimestamp) => this.startTimestamp = startTimestamp;

        public static ValueStopwatch StartNew() => new(Stopwatch.GetTimestamp());

        public TimeSpan GetElapsedTime()
        {
            // Start timestamp can't be zero in an initialized ValueStopwatch. It would have to be literally the first thing executed when the machine boots to be 0.
            // So it being 0 is a clear indication of default(ValueStopwatch)
            if (!IsActive)
            {
                throw new InvalidOperationException("An uninitialized, or 'default', ValueStopwatch cannot be used to get elapsed time.");
            }

            var end = Stopwatch.GetTimestamp();
            var timestampDelta = end - startTimestamp;
            var ticks = (long)(TimestampToTicks * timestampDelta);
            return new TimeSpan(ticks);
        }
    }
}