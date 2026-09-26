/*
   Aardvark Platform
   Copyright (C) 2006-2025  Aardvark Platform Team
   https://aardvark.graphics

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
*/
using Aardvark.Base;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable CS1591

namespace Aardvark.Data.Points
{
    public static class ArrayExtensions
    {
        public static T[] Subset<T>(this T[] xs, int[] subsetIndices) => subsetIndices.MapToArray(i => xs[i]);
        public static T[] Subset<T>(this T[] xs, List<int> subsetIndices) => subsetIndices.MapToArray(i => xs[i]);
        public static T[] Subset<T>(this T[] xs, IList<int> subsetIndices) => ((IReadOnlyList<int>)subsetIndices).MapToArray(i => xs[i]);
        public static T[] Subset<T>(this T[] xs, IReadOnlyList<int> subsetIndices) => subsetIndices.MapToArray(i => xs[i]);
        public static T[] Subset<T>(this IList<T> xs, int[] subsetIndices) => subsetIndices.MapToArray(i => xs[i]);
        public static T[] Subset<T>(this IList<T> xs, List<int> subsetIndices) => subsetIndices.MapToArray(i => xs[i]);
        public static T[] Subset<T>(this IReadOnlyList<T> xs, int[] subsetIndices) => subsetIndices.MapToArray(i => xs[i]);
        public static T[] Subset<T>(this IReadOnlyList<T> xs, IReadOnlyList<int> subsetIndices) => subsetIndices.MapToArray(i => xs[i]);

        public static Array Subset(this object array, IReadOnlyList<int> subsetIndices) => array switch
        {
            Guid   [] xs => Subset(xs, subsetIndices),
            string [] xs => Subset(xs, subsetIndices),
            byte   [] xs => Subset(xs, subsetIndices),
            sbyte  [] xs => Subset(xs, subsetIndices),
            short  [] xs => Subset(xs, subsetIndices),
            ushort [] xs => Subset(xs, subsetIndices),
            int    [] xs => Subset(xs, subsetIndices),
            uint   [] xs => Subset(xs, subsetIndices),
            long   [] xs => Subset(xs, subsetIndices),
            ulong  [] xs => Subset(xs, subsetIndices),
            float  [] xs => Subset(xs, subsetIndices),
            double [] xs => Subset(xs, subsetIndices),
            decimal[] xs => Subset(xs, subsetIndices),
            V2d    [] xs => Subset(xs, subsetIndices),
            V2f    [] xs => Subset(xs, subsetIndices),
            V2i    [] xs => Subset(xs, subsetIndices),
            V2l    [] xs => Subset(xs, subsetIndices),
            V3d    [] xs => Subset(xs, subsetIndices),
            V3f    [] xs => Subset(xs, subsetIndices),
            V3i    [] xs => Subset(xs, subsetIndices),
            V3l    [] xs => Subset(xs, subsetIndices),
            V4d    [] xs => Subset(xs, subsetIndices),
            V4f    [] xs => Subset(xs, subsetIndices),
            V4i    [] xs => Subset(xs, subsetIndices),
            V4l    [] xs => Subset(xs, subsetIndices),
            C3b    [] xs => Subset(xs, subsetIndices),
            C3f    [] xs => Subset(xs, subsetIndices),
            C4b    [] xs => Subset(xs, subsetIndices),
            C4f    [] xs => Subset(xs, subsetIndices),
            M22f   [] xs => Subset(xs, subsetIndices),
            M22d   [] xs => Subset(xs, subsetIndices),
            M33f   [] xs => Subset(xs, subsetIndices),
            M33d   [] xs => Subset(xs, subsetIndices),
            M44f   [] xs => Subset(xs, subsetIndices),
            M44d   [] xs => Subset(xs, subsetIndices),
            Trafo2d[] xs => Subset(xs, subsetIndices),
            Trafo2f[] xs => Subset(xs, subsetIndices),
            Trafo3d[] xs => Subset(xs, subsetIndices),
            Trafo3f[] xs => Subset(xs, subsetIndices),

            IReadOnlyList<Guid   > xs => Subset(xs, subsetIndices),
            IReadOnlyList<string > xs => Subset(xs, subsetIndices),
            IReadOnlyList<byte   > xs => Subset(xs, subsetIndices),
            IReadOnlyList<sbyte  > xs => Subset(xs, subsetIndices),
            IReadOnlyList<short  > xs => Subset(xs, subsetIndices),
            IReadOnlyList<ushort > xs => Subset(xs, subsetIndices),
            IReadOnlyList<int    > xs => Subset(xs, subsetIndices),
            IReadOnlyList<uint   > xs => Subset(xs, subsetIndices),
            IReadOnlyList<long   > xs => Subset(xs, subsetIndices),
            IReadOnlyList<ulong  > xs => Subset(xs, subsetIndices),
            IReadOnlyList<float  > xs => Subset(xs, subsetIndices),
            IReadOnlyList<double > xs => Subset(xs, subsetIndices),
            IReadOnlyList<decimal> xs => Subset(xs, subsetIndices),
            IReadOnlyList<V2d    > xs => Subset(xs, subsetIndices),
            IReadOnlyList<V2f    > xs => Subset(xs, subsetIndices),
            IReadOnlyList<V2i    > xs => Subset(xs, subsetIndices),
            IReadOnlyList<V2l    > xs => Subset(xs, subsetIndices),
            IReadOnlyList<V3d    > xs => Subset(xs, subsetIndices),
            IReadOnlyList<V3f    > xs => Subset(xs, subsetIndices),
            IReadOnlyList<V3i    > xs => Subset(xs, subsetIndices),
            IReadOnlyList<V3l    > xs => Subset(xs, subsetIndices),
            IReadOnlyList<V4d    > xs => Subset(xs, subsetIndices),
            IReadOnlyList<V4f    > xs => Subset(xs, subsetIndices),
            IReadOnlyList<V4i    > xs => Subset(xs, subsetIndices),
            IReadOnlyList<V4l    > xs => Subset(xs, subsetIndices),
            IReadOnlyList<C3b    > xs => Subset(xs, subsetIndices),
            IReadOnlyList<C3f    > xs => Subset(xs, subsetIndices),
            IReadOnlyList<C4b    > xs => Subset(xs, subsetIndices),
            IReadOnlyList<C4f    > xs => Subset(xs, subsetIndices),
            IReadOnlyList<M22f   > xs => Subset(xs, subsetIndices),
            IReadOnlyList<M22d   > xs => Subset(xs, subsetIndices),
            IReadOnlyList<M33f   > xs => Subset(xs, subsetIndices),
            IReadOnlyList<M33d   > xs => Subset(xs, subsetIndices),
            IReadOnlyList<M44f   > xs => Subset(xs, subsetIndices),
            IReadOnlyList<M44d   > xs => Subset(xs, subsetIndices),
            IReadOnlyList<Trafo2d> xs => Subset(xs, subsetIndices),
            IReadOnlyList<Trafo2f> xs => Subset(xs, subsetIndices),
            IReadOnlyList<Trafo3d> xs => Subset(xs, subsetIndices),
            IReadOnlyList<Trafo3f> xs => Subset(xs, subsetIndices),

            _ => throw new Exception($"Type {array.GetType()} is not supported.")
        };
    }

    public static class EnumerableExtensions
    {
        internal static R[] MapToArray<T, R>(this IReadOnlyList<T> xs, Func<T, R> map)
        {
            var rs = new R[xs.Count];
            for (var i = 0; i < rs.Length; i++) rs[i] = map(xs[i]);
            return rs;
        }

        /// <summary>
        /// Maps a sequence concurrently and yields results in worker-completion order. At most
        /// the effective parallelism number of source items, workers, and completed results are
        /// retained at any time. Null results are yielded like any other result.
        /// </summary>
        /// <remarks>
        /// A non-positive <paramref name="maxLevelOfParallelism"/> uses
        /// <see cref="Environment.ProcessorCount"/>. Worker and source exceptions are rethrown
        /// without an aggregate wrapper. Failure, external cancellation, or early disposal of
        /// the returned enumerator cancels the linked token supplied to all in-flight workers;
        /// mapping functions should observe that token for prompt shutdown. <paramref name="onFinish"/>
        /// is invoked once only after successful complete enumeration.
        /// </remarks>
        /// <param name="items">The lazily consumed source sequence.</param>
        /// <param name="map">The mapping function. Its token links external and pipeline cancellation.</param>
        /// <param name="maxLevelOfParallelism">Maximum concurrent mappings, or a non-positive value for the processor count.</param>
        /// <param name="onFinish">Optional callback invoked after successful complete enumeration.</param>
        /// <param name="ct">External cancellation token.</param>
        /// <returns>A lazy sequence of mapped results in completion order.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="map"/> is null.</exception>
        public static IEnumerable<R> MapParallel<T, R>(this IEnumerable<T> items,
            Func<T, CancellationToken, R> map,
            int maxLevelOfParallelism,
            Action<TimeSpan>? onFinish = null,
            CancellationToken ct = default
            ) where R : class
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (maxLevelOfParallelism < 1) maxLevelOfParallelism = Environment.ProcessorCount;

            var completions = new Queue<MapParallelCompletion<R>>(maxLevelOfParallelism);
            var completionAvailable = new SemaphoreSlim(0, maxLevelOfParallelism);
            var workers = new Task?[maxLevelOfParallelism];
            var workerCount = 0;
            var linkedCancellation = ct.CanBeCanceled
                ? CancellationTokenSource.CreateLinkedTokenSource(ct)
                : new CancellationTokenSource();
            var sw = Stopwatch.StartNew();
            var completedSuccessfully = false;
            IEnumerator<T>? enumerator = null;

            try
            {
                enumerator = items.GetEnumerator();
                var sourceCompleted = false;

                while (true)
                {
                    ct.ThrowIfCancellationRequested();

                    MapParallelCompletion<R>? completion = null;
                    lock (completions)
                    {
                        if (completions.Count > 0)
                        {
                            completion = completions.Dequeue();
                            completionAvailable.Wait(0);
                        }
                    }

                    if (completion.HasValue)
                    {
                        var value = completion.Value;
                        workers[value.Slot]!.GetAwaiter().GetResult();
                        workers[value.Slot] = null;
                        workerCount--;
                        value.Error?.Throw();
                        yield return value.Result!;
                        continue;
                    }

                    if (!sourceCompleted && workerCount < maxLevelOfParallelism)
                    {
                        T item;
                        try
                        {
                            if (enumerator!.MoveNext())
                            {
                                item = enumerator.Current;
                            }
                            else
                            {
                                sourceCompleted = true;
                                var finishedEnumerator = enumerator;
                                enumerator = null;
                                finishedEnumerator.Dispose();
                                continue;
                            }
                        }
                        catch
                        {
                            CancelWithoutThrowing(linkedCancellation);
                            if (enumerator != null)
                            {
                                try { enumerator.Dispose(); }
                                catch { }
                                enumerator = null;
                            }
                            throw;
                        }

                        var slot = 0;
                        while (workers[slot] != null) slot++;

                        var worker = Task.Run(() =>
                        {
                            R? result = null;
                            ExceptionDispatchInfo? error = null;
                            try
                            {
                                result = map(item, linkedCancellation.Token);
                                linkedCancellation.Token.ThrowIfCancellationRequested();
                            }
                            catch (Exception e)
                            {
                                error = ExceptionDispatchInfo.Capture(e);
                            }

                            lock (completions)
                            {
                                completions.Enqueue(new MapParallelCompletion<R>(slot, result, error));
                                completionAvailable.Release();
                            }

                            if (error != null) CancelWithoutThrowing(linkedCancellation);
                        });
                        workers[slot] = worker;
                        workerCount++;
                        continue;
                    }

                    if (workerCount > 0)
                    {
                        completionAvailable.Wait(ct);
                        continue;
                    }

                    break;
                }

                sw.Stop();
                onFinish?.Invoke(sw.Elapsed);
                completedSuccessfully = true;
            }
            finally
            {
                if (!completedSuccessfully) CancelWithoutThrowing(linkedCancellation);

                try
                {
                    enumerator?.Dispose();
                }
                finally
                {
                    if (workerCount == 0)
                    {
                        completionAvailable.Dispose();
                        linkedCancellation.Dispose();
                    }
                    else
                    {
                        var pending = new Task[workerCount];
                        for (int i = 0, j = 0; i < workers.Length; i++)
                            if (workers[i] != null) pending[j++] = workers[i]!;

                        _ = Task.WhenAll(pending).ContinueWith(
                            t =>
                            {
                                try
                                {
                                    if (t.IsFaulted) _ = t.Exception;
                                    completionAvailable.Dispose();
                                    linkedCancellation.Dispose();
                                }
                                catch { }
                            },
                            CancellationToken.None,
                            TaskContinuationOptions.ExecuteSynchronously,
                            TaskScheduler.Default
                            );
                    }
                }
            }
        }

        private readonly struct MapParallelCompletion<R> where R : class
        {
            public MapParallelCompletion(int slot, R? result, ExceptionDispatchInfo? error)
            {
                Slot = slot;
                Result = result;
                Error = error;
            }

            public readonly int Slot;
            public readonly R? Result;
            public readonly ExceptionDispatchInfo? Error;
        }

        private static void CancelWithoutThrowing(CancellationTokenSource cancellation)
        {
            try { cancellation.Cancel(); }
            catch (ObjectDisposedException) { }
            catch (AggregateException) { }
        }
    }
}
