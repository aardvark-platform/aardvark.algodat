/*
    Copyright (C) 2006-2026. Aardvark Platform Team. http://github.com/aardvark-platform.
    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU Affero General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.
    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Affero General Public License for more details.
    You should have received a copy of the GNU Affero General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/
using Aardvark.Base;
using Aardvark.Data.Points;
using Aardvark.Geometry.Points;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class MapParallelTests
    {
        private sealed class Result
        {
            public Result(int value) => Value = value;
            public int Value { get; }
        }

        private sealed class TestException : Exception
        {
            public TestException(string message) : base(message) { }
        }

        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        private static Task<T[]> EnumerateAsync<T>(IEnumerable<T> source)
            => Task.Factory.StartNew(
                () => source.ToArray(),
                CancellationToken.None,
                TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach,
                TaskScheduler.Default
                );

        private static void Wait(ManualResetEventSlim signal, string message)
            => Assert.That(signal.Wait(Timeout), Is.True, message);

        private static void Wait(CountdownEvent signal, string message)
            => Assert.That(signal.Wait(Timeout), Is.True, message);

        private static T Await<T>(Task<T> task)
            => task.WaitAsync(Timeout).GetAwaiter().GetResult();

        private static IEnumerable<int> CountedSource(int count, Action onRead)
        {
            for (var i = 0; i < count; i++)
            {
                onRead();
                yield return i;
            }
        }

        private static void UpdateMaximum(ref int maximum, int value)
        {
            var current = Volatile.Read(ref maximum);
            while (value > current)
            {
                var previous = Interlocked.CompareExchange(ref maximum, value, current);
                if (previous == current) return;
                current = previous;
            }
        }

        [Test]
        public void EmptySourceIsLazyAndFinishesSuccessfully()
        {
            var sourceMoves = 0;
            var mapCalls = 0;
            var finishCalls = 0;

            IEnumerable<int> Source()
            {
                Interlocked.Increment(ref sourceMoves);
                yield break;
            }

            var mapped = Source().MapParallel(
                (x, ct) =>
                {
                    Interlocked.Increment(ref mapCalls);
                    return new Result(x);
                },
                maxLevelOfParallelism: 2,
                onFinish: _ => Interlocked.Increment(ref finishCalls)
                );

            Assert.That(sourceMoves, Is.Zero);
            Assert.That(mapCalls, Is.Zero);
            Assert.That(finishCalls, Is.Zero);

            using var enumerator = mapped.GetEnumerator();
            Assert.That(sourceMoves, Is.Zero);
            Assert.That(enumerator.MoveNext(), Is.False);
            Assert.That(sourceMoves, Is.EqualTo(1));
            Assert.That(mapCalls, Is.Zero);
            Assert.That(finishCalls, Is.EqualTo(1));
        }

        [Test]
        public void SerialParallelismNeverOverlapsWorkers()
        {
            using var firstStarted = new ManualResetEventSlim(false);
            using var releaseFirst = new ManualResetEventSlim(false);
            var reads = 0;
            var active = 0;
            var maximum = 0;

            Result Map(int value, CancellationToken ct)
            {
                var now = Interlocked.Increment(ref active);
                UpdateMaximum(ref maximum, now);
                try
                {
                    if (value == 0)
                    {
                        firstStarted.Set();
                        releaseFirst.Wait(ct);
                    }
                    return new Result(value);
                }
                finally
                {
                    Interlocked.Decrement(ref active);
                }
            }

            var task = EnumerateAsync(
                CountedSource(4, () => Interlocked.Increment(ref reads)).MapParallel(Map, 1)
                );
            Wait(firstStarted, "The first worker did not start.");

            Assert.That(reads, Is.EqualTo(1));
            Assert.That(active, Is.EqualTo(1));
            Assert.That(maximum, Is.EqualTo(1));

            releaseFirst.Set();
            var results = Await(task);
            Assert.That(results.Select(x => x.Value), Is.EqualTo(new[] { 0, 1, 2, 3 }));
            Assert.That(maximum, Is.EqualTo(1));
        }

        [Test]
        public void ConfiguredParallelismBoundsOverlap()
        {
            const int parallelism = 3;
            using var started = new CountdownEvent(parallelism);
            using var release = new ManualResetEventSlim(false);
            var reads = 0;
            var active = 0;
            var maximum = 0;

            Result Map(int value, CancellationToken ct)
            {
                var now = Interlocked.Increment(ref active);
                UpdateMaximum(ref maximum, now);
                if (value < parallelism) started.Signal();
                try
                {
                    release.Wait(ct);
                    return new Result(value);
                }
                finally
                {
                    Interlocked.Decrement(ref active);
                }
            }

            var task = EnumerateAsync(
                CountedSource(12, () => Interlocked.Increment(ref reads)).MapParallel(Map, parallelism)
                );
            Wait(started, "The configured workers did not overlap.");

            Assert.That(reads, Is.EqualTo(parallelism));
            Assert.That(active, Is.EqualTo(parallelism));
            Assert.That(maximum, Is.EqualTo(parallelism));

            release.Set();
            var results = Await(task);
            Assert.That(results.Length, Is.EqualTo(12));
            Assert.That(maximum, Is.EqualTo(parallelism));
        }

        [Test]
        public void ResultsAreYieldedInCompletionOrder()
        {
            using var started = new CountdownEvent(3);
            var release = Enumerable.Range(0, 3).Select(_ => new ManualResetEventSlim(false)).ToArray();
            var yielded = Enumerable.Range(0, 3).Select(_ => new ManualResetEventSlim(false)).ToArray();
            try
            {
                Result Map(int value, CancellationToken ct)
                {
                    started.Signal();
                    release[value].Wait(ct);
                    return new Result(value);
                }

                var task = EnumerateAsync(
                    Enumerable.Range(0, 3)
                        .MapParallel(Map, 3)
                        .Select(x =>
                        {
                            yielded[x.Value].Set();
                            return x;
                        })
                    );
                Wait(started, "Workers did not start.");

                release[2].Set();
                Wait(yielded[2], "The last input was not yielded first.");
                release[0].Set();
                Wait(yielded[0], "The first input was not yielded second.");
                release[1].Set();

                var results = Await(task);
                Assert.That(results.Select(x => x.Value), Is.EqualTo(new[] { 2, 0, 1 }));
            }
            finally
            {
                foreach (var signal in release) signal.Set();
                foreach (var signal in release) signal.Dispose();
                foreach (var signal in yielded) signal.Dispose();
            }
        }

        [Test]
        public void EveryResultIncludingNullIsDeliveredExactlyOnce()
        {
            const int count = 128;
            var calls = new int[count];

            Result Map(int value, CancellationToken ct)
            {
                Interlocked.Increment(ref calls[value]);
                return value % 11 == 0 ? null! : new Result(value);
            }

            var results = Enumerable.Range(0, count).MapParallel(Map, 4).ToArray();

            Assert.That(results.Length, Is.EqualTo(count));
            Assert.That(results.Count(x => x == null), Is.EqualTo(12));
            Assert.That(calls, Is.All.EqualTo(1));
            Assert.That(
                results.Where(x => x != null).Select(x => x.Value).OrderBy(x => x),
                Is.EqualTo(Enumerable.Range(0, count).Where(x => x % 11 != 0))
                );
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void NonPositiveParallelismUsesProcessorCount(int configuredParallelism)
        {
            var effectiveParallelism = Environment.ProcessorCount;
            using var started = new CountdownEvent(effectiveParallelism);
            using var release = new ManualResetEventSlim(false);
            var reads = 0;
            var active = 0;
            var maximum = 0;

            Result Map(int value, CancellationToken ct)
            {
                var now = Interlocked.Increment(ref active);
                UpdateMaximum(ref maximum, now);
                if (value < effectiveParallelism) started.Signal();
                try
                {
                    release.Wait(ct);
                    return new Result(value);
                }
                finally
                {
                    Interlocked.Decrement(ref active);
                }
            }

            var task = EnumerateAsync(
                CountedSource(effectiveParallelism + 1, () => Interlocked.Increment(ref reads))
                    .MapParallel(Map, configuredParallelism)
                );
            Wait(started, "The default number of workers did not start.");

            Assert.That(reads, Is.EqualTo(effectiveParallelism));
            Assert.That(maximum, Is.EqualTo(effectiveParallelism));

            release.Set();
            Assert.That(Await(task).Length, Is.EqualTo(effectiveParallelism + 1));
        }

        [Test]
        public void SourcePrefetchIsBoundedByParallelism()
        {
            const int parallelism = 2;
            using var started = new CountdownEvent(parallelism);
            using var release = new ManualResetEventSlim(false);
            var reads = 0;

            Result Map(int value, CancellationToken ct)
            {
                if (value < parallelism) started.Signal();
                release.Wait(ct);
                return new Result(value);
            }

            var task = EnumerateAsync(
                CountedSource(1000, () => Interlocked.Increment(ref reads)).MapParallel(Map, parallelism)
                );
            Wait(started, "Workers did not occupy pipeline capacity.");
            Assert.That(reads, Is.EqualTo(parallelism));

            release.Set();
            Assert.That(Await(task).Length, Is.EqualTo(1000));
        }

        [Test]
        public void WorkerFailureIsRethrownAndStopsSourceConsumption()
        {
            var expected = new TestException("worker failure");
            using var started = new CountdownEvent(2);
            using var peerCancelled = new ManualResetEventSlim(false);
            using var emergencyRelease = new ManualResetEventSlim(false);
            var reads = 0;
            var finishCalls = 0;

            Result Map(int value, CancellationToken ct)
            {
                started.Signal();
                Wait(started, "Workers did not start before failure.");
                if (value == 0) throw expected;

                WaitHandle.WaitAny(new[] { ct.WaitHandle, emergencyRelease.WaitHandle });
                if (ct.IsCancellationRequested) peerCancelled.Set();
                ct.ThrowIfCancellationRequested();
                return new Result(value);
            }

            var task = EnumerateAsync(
                CountedSource(100, () => Interlocked.Increment(ref reads))
                    .MapParallel(Map, 2, _ => Interlocked.Increment(ref finishCalls))
                );

            try
            {
                var thrown = Assert.Throws<TestException>(() => Await(task));
                Assert.That(thrown, Is.SameAs(expected));
                Wait(peerCancelled, "The peer worker did not receive linked cancellation.");
                Assert.That(reads, Is.EqualTo(2));
                Assert.That(finishCalls, Is.Zero);
            }
            finally
            {
                emergencyRelease.Set();
            }
        }

        [Test]
        public void SourceEnumeratorFailureCancelsWorkersAndPreservesException()
        {
            var expected = new TestException("source failure");
            using var workersStarted = new CountdownEvent(2);
            using var workersCancelled = new CountdownEvent(2);
            using var emergencyRelease = new ManualResetEventSlim(false);

            IEnumerable<int> Source()
            {
                yield return 0;
                yield return 1;
                throw expected;
            }

            Result Map(int value, CancellationToken ct)
            {
                workersStarted.Signal();
                WaitHandle.WaitAny(new[] { ct.WaitHandle, emergencyRelease.WaitHandle });
                if (ct.IsCancellationRequested) workersCancelled.Signal();
                ct.ThrowIfCancellationRequested();
                return new Result(value);
            }

            var task = EnumerateAsync(Source().MapParallel(Map, 3));
            try
            {
                var thrown = Assert.Throws<TestException>(() => Await(task));
                Assert.That(thrown, Is.SameAs(expected));
                Wait(workersStarted, "Source enumeration failed before workers started.");
                Wait(workersCancelled, "Source failure did not cancel all workers.");
            }
            finally
            {
                emergencyRelease.Set();
            }
        }

        [Test]
        public void ExternalCancellationInterruptsCapacityWaitAndCancelsWorkers()
        {
            using var cancellation = new CancellationTokenSource();
            using var workersStarted = new CountdownEvent(2);
            using var workersStopped = new CountdownEvent(2);
            using var emergencyRelease = new ManualResetEventSlim(false);
            var reads = 0;

            Result Map(int value, CancellationToken ct)
            {
                workersStarted.Signal();
                try
                {
                    WaitHandle.WaitAny(new[] { ct.WaitHandle, emergencyRelease.WaitHandle });
                    ct.ThrowIfCancellationRequested();
                    return new Result(value);
                }
                finally
                {
                    workersStopped.Signal();
                }
            }

            var task = EnumerateAsync(
                CountedSource(100, () => Interlocked.Increment(ref reads))
                    .MapParallel(Map, 2, ct: cancellation.Token)
                );
            try
            {
                Wait(workersStarted, "Workers did not occupy pipeline capacity.");
                Assert.That(reads, Is.EqualTo(2));

                cancellation.Cancel();
                var thrown = Assert.Throws<OperationCanceledException>(() => Await(task));
                Assert.That(thrown!.CancellationToken, Is.EqualTo(cancellation.Token));
                Wait(workersStopped, "External cancellation did not stop all workers.");
                Assert.That(reads, Is.EqualTo(2));
            }
            finally
            {
                emergencyRelease.Set();
            }
        }

        [Test]
        public void DisposingEnumeratorCancelsInFlightWorkers()
        {
            using var workersStarted = new CountdownEvent(2);
            using var peerCancelled = new ManualResetEventSlim(false);
            using var emergencyRelease = new ManualResetEventSlim(false);
            var finishCalls = 0;

            Result Map(int value, CancellationToken ct)
            {
                workersStarted.Signal();
                Wait(workersStarted, "Workers did not start.");
                if (value == 0) return new Result(value);

                WaitHandle.WaitAny(new[] { ct.WaitHandle, emergencyRelease.WaitHandle });
                if (ct.IsCancellationRequested) peerCancelled.Set();
                ct.ThrowIfCancellationRequested();
                return new Result(value);
            }

            var enumerator = Enumerable.Range(0, 10)
                .MapParallel(Map, 2, _ => Interlocked.Increment(ref finishCalls))
                .GetEnumerator();
            try
            {
                Assert.That(enumerator.MoveNext(), Is.True);
                Assert.That(enumerator.Current.Value, Is.EqualTo(0));
            }
            finally
            {
                enumerator.Dispose();
            }

            try
            {
                Wait(peerCancelled, "Enumerator disposal did not cancel the peer worker.");
                Assert.That(finishCalls, Is.Zero);
            }
            finally
            {
                emergencyRelease.Set();
            }
        }

        [Test]
        public void OnFinishRunsOnceAfterSuccessfulCompletion()
        {
            var mapped = 0;
            var finishCalls = 0;
            var mappedAtFinish = -1;
            var elapsed = TimeSpan.MinValue;

            var results = Enumerable.Range(0, 32).MapParallel(
                (value, ct) =>
                {
                    Interlocked.Increment(ref mapped);
                    return new Result(value);
                },
                maxLevelOfParallelism: 4,
                onFinish: duration =>
                {
                    elapsed = duration;
                    mappedAtFinish = Volatile.Read(ref mapped);
                    Interlocked.Increment(ref finishCalls);
                }
                ).ToArray();

            Assert.That(results.Length, Is.EqualTo(32));
            Assert.That(mapped, Is.EqualTo(32));
            Assert.That(mappedAtFinish, Is.EqualTo(32));
            Assert.That(finishCalls, Is.EqualTo(1));
            Assert.That(elapsed, Is.GreaterThanOrEqualTo(TimeSpan.Zero));
        }

        [Test]
        public void ReprojectedChunkImportRetainsEveryMappedPoint()
        {
            const int chunkCount = 24;
            const int pointsPerChunk = 3;
            const int parallelism = 4;
            using var initialWorkers = new CountdownEvent(parallelism);
            var reprojectCalls = 0;

            var chunks = Enumerable.Range(0, chunkCount).Select(chunkIndex =>
            {
                var x = chunkIndex * 10.0;
                return new Chunk(new[]
                {
                    new V3d(x, 0, 0),
                    new V3d(x + 1, 1, 3),
                    new V3d(x + 2, 3, 2),
                });
            }).ToArray();

            IList<V3d> Reproject(IList<V3d> positions)
            {
                var call = Interlocked.Increment(ref reprojectCalls);
                if (call <= parallelism)
                {
                    initialWorkers.Signal();
                    Wait(initialWorkers, "Reprojection workers did not overlap.");
                }
                return positions.Select(p => new V3d(p.X, p.Z, p.Y)).ToArray();
            }

            var expected = chunks
                .SelectMany(chunk => chunk.Positions)
                .Select(p => new V3d(p.X, p.Z, p.Y))
                .OrderBy(p => p.X).ThenBy(p => p.Y).ThenBy(p => p.Z)
                .ToArray();
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("map-parallel-reprojection")
                .WithMaxDegreeOfParallelism(parallelism)
                .WithMaxChunkPointCount(12)
                .WithOctreeSplitLimit(8)
                .WithMinDist(0.0)
                .WithReproject(Reproject);

            var pointSet = PointCloud.Chunks(chunks, config);
            var actual = pointSet.QueryAllPoints()
                .SelectMany(chunk => chunk.Positions)
                .OrderBy(p => p.X).ThenBy(p => p.Y).ThenBy(p => p.Z)
                .ToArray();

            Assert.That(reprojectCalls, Is.EqualTo(chunkCount));
            Assert.That(pointSet.PointCount, Is.EqualTo(chunkCount * pointsPerChunk));
            Assert.That(actual, Is.EqualTo(expected));
        }
    }
}
