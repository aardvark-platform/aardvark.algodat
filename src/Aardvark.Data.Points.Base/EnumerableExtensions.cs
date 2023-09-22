/*
   Aardvark Platform
   Copyright (C) 2006-2023  Aardvark Platform Team
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
using System.Diagnostics.CodeAnalysis;
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

        public static IEnumerable<R> MapParallel<T, R>(this IEnumerable<T> items,
            Func<T, CancellationToken, R> map,
            int maxLevelOfParallelism,
            Action<TimeSpan>? onFinish = null,
            CancellationToken ct = default
            ) where R : class
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (maxLevelOfParallelism < 1) maxLevelOfParallelism = Environment.ProcessorCount;

            var queue = new Queue<R>();
            var queueSemapore = new SemaphoreSlim(maxLevelOfParallelism);

            var inFlightCount = 0;

            var sw = new Stopwatch(); sw.Start();

            var ts = new List<Task>();
            foreach (var item in items)
            {
                ct.ThrowIfCancellationRequested();

                queueSemapore.Wait();
                ct.ThrowIfCancellationRequested();
                Interlocked.Increment(ref inFlightCount);
                ts.Add(Task.Run(() =>
                {
                    try
                    {
                        var r = map(item, ct);
                        ct.ThrowIfCancellationRequested();
                        lock (queue) queue.Enqueue(r);
                    }
                    finally
                    {
                        Interlocked.Decrement(ref inFlightCount);
                        queueSemapore.Release();
                    }
                }));

                while (queue.TryDequeue(out R? r)) { ct.ThrowIfCancellationRequested(); yield return r; }
            }

            while (inFlightCount > 0 || queue.Count > 0)
            {
                while (queue.TryDequeue(out R? r)) { ct.ThrowIfCancellationRequested(); yield return r; }
                Task.Delay(100).Wait();
            }

            Task.WaitAll(ts.ToArray());

            sw.Stop();
            onFinish?.Invoke(sw.Elapsed);
        }

        private static bool TryDequeue<T>(this Queue<T> queue, [NotNullWhen(true)] out T? item) where T : class
        {
            lock (queue)
            {
                if (queue.Count > 0)
                {
                    item = queue.Dequeue();
                    return true;
                }
                else
                {
                    item = default;
                    return false;
                }
            }
        }
    }
}
