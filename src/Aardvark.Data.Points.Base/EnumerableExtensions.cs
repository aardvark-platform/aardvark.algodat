/*
   Aardvark Platform
   Copyright (C) 2006-2020  Aardvark Platform Team
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
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Aardvark.Data.Points
{
    /// <summary>
    /// </summary>
    public static class EnumerableExtensions
    {
        internal static R[] MapToArray<T, R>(this IList<T> xs, Func<T, R> map)
        {
            var rs = new R[xs.Count];
            for (var i = 0; i < rs.Length; i++) rs[i] = map(xs[i]);
            return rs;
        }

        /// <summary>
        /// </summary>
        public static IEnumerable<R?> MapParallel<T, R>(this IEnumerable<T> items,
            Func<T, CancellationToken, R> map,
            int maxLevelOfParallelism,
            Action<TimeSpan>? onFinish = null,
            CancellationToken ct = default
            ) where R : class
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (maxLevelOfParallelism < 1) maxLevelOfParallelism = Environment.ProcessorCount;

            var queue = new Queue<R?>();
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
                        queueSemapore.Release();
                    }
                });

                while (queue.TryDequeue(out R? r)) { ct.ThrowIfCancellationRequested(); yield return r; }
            }

            while (inFlightCount > 0 || queue.Count > 0)
            {
                while (queue.TryDequeue(out R? r)) { ct.ThrowIfCancellationRequested(); yield return r; }
                Task.Delay(100).Wait();
            }

            sw.Stop();
            onFinish?.Invoke(sw.Elapsed);
        }

        private static bool TryDequeue<T>(this Queue<T?> queue, out T? item) where T : class
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
