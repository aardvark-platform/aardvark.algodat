/*
    Copyright (C) 2006-2018. Aardvark Platform Team. http://github.com/aardvark-platform.
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

        internal static T[] Append<T>(this T[] self, T[] other)
        {
            if (self == null || self.Length == 0) return other ?? new T[0];
            if (other == null || other.Length == 0) return self;

            var xs = new T[self.Length + other.Length];
            for (var i = 0; i < self.Length; i++) xs[i] = self[i];
            for (var j = 0; j < other.Length; j++) xs[self.Length + j] = other[j];
            return xs;
        }

        internal static T[] Take<T>(this T[] self, int count)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            if (self == null || count == 0) return new T[0];
            if (self.Length <= count) return self;

            var xs = new T[count];
            for (var i = 0; i < count; i++) xs[i] = self[i];
            return xs;
        }

        internal static T[] Reordered<T>(this T[] self, int[] ia)
        {
            if (self == null) throw new ArgumentNullException(nameof(self));
            if (ia == null) throw new ArgumentNullException(nameof(self));
            if (self.Length != ia.Length) throw new ArgumentException(nameof(ia));

            var xs = new T[ia.Length];
            for (var i = 0; i < ia.Length; i++) xs[i] = self[ia[i]];
            return xs;
        }

        /// <summary>
        /// </summary>
        public static T MapReduceParallel<T>(this IEnumerable<T> xs,
            Func<T, T, CancellationToken, T> reduce,
            int maxLevelOfParallelism,
            Action<TimeSpan> onFinish = null,
            CancellationToken ct = default
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

                T a, b;
                lock (queue)
                {
                    a = queue.Dequeue();
                    b = queue.Dequeue();
                    if (a == null || b == null) throw new InvalidOperationException();
                }

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
    }
}
