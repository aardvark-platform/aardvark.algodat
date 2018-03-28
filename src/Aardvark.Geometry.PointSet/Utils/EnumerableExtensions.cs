/*
    Copyright (C) 2017. Aardvark Platform Team. http://github.com/aardvark-platform.
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
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aardvark.Base;

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// </summary>
    public static class EnumerableExtensions
    {
        private static bool TryDequeue<T>(this Queue<T> queue, out T item)
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
                    item = default(T);
                    return false;
                }
            }
        }

        internal static bool Any<T>(this IEnumerable<T> xs, Func<T, int, bool> predicate)
        {
            var i = 0;
            foreach (var x in xs) if (predicate(x, i++)) return true;
            return false;
        }

        internal static bool Any<T>(this T[] xs, Func<T, int, bool> predicate)
        {
            for (var i = 0; i < xs.Length; i++) if (predicate(xs[i], i)) return true;
            return false;
        }

        internal static R[] Map<T, R>(this IList<T> xs, Func<T, R> map)
        {
            var rs = new R[xs.Count];
            for (var i = 0; i < rs.Length; i++) rs[i] = map(xs[i]);
            return rs;
        }

        /// <summary>
        /// </summary>
        public static T MapReduceParallel<T>(this IEnumerable<T> xs,
            Func<T, T, CancellationToken, T> reduce,
            int maxLevelOfParallelism,
            Action<TimeSpan> onFinish = null,
            CancellationToken ct = default(CancellationToken)
            )
        {
            if (maxLevelOfParallelism < 1) maxLevelOfParallelism = Environment.ProcessorCount;

            var queue = new Queue<T>(xs);
            var imax = queue.Count - 1;
            var queueSemapore = new SemaphoreSlim(maxLevelOfParallelism);
            var exception = default(Exception);

            var inFlightCount = 0;

            var sw = new Stopwatch(); sw.Start();

            for (var i = 0; i < imax; i++)
            {
                while (queue.Count < 2)
                {
                    CheckCancellationOrException();
                    Task.Delay(100).Wait();
                }

                var a = queue.Dequeue();
                var b = queue.Dequeue();

                queueSemapore.Wait();
                CheckCancellationOrException();

                Interlocked.Increment(ref inFlightCount);
                Task.Run(() =>
                {
                    try
                    {
                        var r = reduce(a, b, ct);
                        if (r == null) Debugger.Break();
                        CheckCancellationOrException();
                        lock (queue) queue.Enqueue(r);
                    }
                    catch (Exception e)
                    {
                        Report.Error($"{e}");
                        exception = e;
                        throw;
                    }
                    finally
                    {
                        Interlocked.Decrement(ref inFlightCount);
                    }
                });
                queueSemapore.Release();
            }

            while (inFlightCount > 0) { CheckCancellationOrException(); Task.Delay(100).Wait(); }
            if (queue.Count != 1) { CheckCancellationOrException(); throw new InvalidOperationException(); }
            var result = queue.Dequeue();
            
            sw.Stop();
            onFinish?.Invoke(sw.Elapsed);

            return result;

            void CheckCancellationOrException()
            {
                ct.ThrowIfCancellationRequested();
                if (exception != null) throw new Exception("MapReduceParallel failed. See inner exception.", exception);
            }
        }

        internal static IEnumerable<R> MapParallel<T, R>(this IEnumerable<T> items,
            Func<T, CancellationToken, R> map,
            int maxLevelOfParallelism,
            Action<TimeSpan> onFinish = null,
            CancellationToken ct = default(CancellationToken)
            )
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (maxLevelOfParallelism < 1) maxLevelOfParallelism = Environment.ProcessorCount;

            var queue = new Queue<R>();
            var queueSemapore = new SemaphoreSlim(maxLevelOfParallelism);
            
            var inFlightCount = 0;

            var sw = new Stopwatch(); sw.Start();
            
            foreach (var item in items)
            {
                ct.ThrowIfCancellationRequested();

                queueSemapore.Wait();
                ct.ThrowIfCancellationRequested();
                Interlocked.Increment(ref inFlightCount);
                Task.Run(() =>
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
                    }
                });
                queueSemapore.Release();

                while (queue.TryDequeue(out R r)) { ct.ThrowIfCancellationRequested(); yield return r; }
            }
            
            while (inFlightCount > 0 || queue.Count > 0)
            {
                while (queue.TryDequeue(out R r)) { ct.ThrowIfCancellationRequested(); yield return r; }
                Task.Delay(100).Wait();
            }

            sw.Stop();
            onFinish?.Invoke(sw.Elapsed);
        }
    }
}
