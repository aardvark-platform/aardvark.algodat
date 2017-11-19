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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aardvark.Base;

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// Importer for sequence of chunks of points.
    /// </summary>
    [Obsolete]
    internal static class LegacyImporter
    {
        /// <summary>
        /// Imports point data into store.
        /// </summary>
        /// <param name="storage">Store to import pointset into.</param>
        /// <param name="key">Store key for imported pointset.</param>
        /// <param name="parser">Parser produces sequence of chunks of points.</param>
        /// <param name="estimatedChunkCount">Estimated total number of chunks.</param>
        /// <param name="octreeSplitLimit">Split limit for octree.</param>
        /// <param name="maxLevelOfParallelism">Processes this number of chunks in parallel.</param>
        /// <param name="progress">Progress callback.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns></returns>
        [Obsolete]
        public static async Task<PointSet> Import(
            Storage storage,
            string key,
            Func<Action<Chunk>, Task> parser,
            int estimatedChunkCount,
            int octreeSplitLimit,
            int maxLevelOfParallelism,
            Action<double> progress,
            CancellationToken ct
            )
        {
            using (Timing.Do($"[Importer   ] importing"))
            {
                #region parse point data
                var chunks = new Queue<PointSet>();
                using (Timing.Do($"[Importer   ] parsing point data"))
                {
                    var totalPointCount = 0L;
                    var chunksSeenSoFar = 0L;

                    await parser(chunk =>
                    {
                        var sequenceNumber = Interlocked.Increment(ref chunksSeenSoFar);
                        progress?.Invoke(sequenceNumber / estimatedChunkCount / 3.0);

                        if (chunk.Positions.Count == 0) return;

                        var ps = chunk.Positions;
                        var cs = chunk.Colors;
                        var psHash = ps.ComputeMd5Hash();
                        var csHash = cs.ComputeMd5Hash();

                        try
                        {
                            var id = $"{key}_{sequenceNumber}.json";
                            var pointset = PointSet.Create(storage, id, ps, cs, octreeSplitLimit, true, ct);
                            storage.Add(pointset.Id, pointset, ct);
                            chunks.Enqueue(pointset);

                            var count = Interlocked.Add(ref totalPointCount, pointset.Root.Value.PointCountTree);
                            Console.WriteLine($"[Importer   ] created chunk from {ps.Count:#,#} points");
                        }
                        catch (Exception e)
                        {
                            Report.Error($"[Importer   ] {ps.Count}; {psHash}; {csHash}; {e}");
                        }
                    });

                    ct.ThrowIfCancellationRequested();
                }
                #endregion

                #region merging chunks
                PointSet final;
                using (Timing.Do($"[Importer   ] merging chunks"))
                {
                    var pointSetsCount = (double)chunks.Count;
                    var semaphore = new SemaphoreSlim(maxLevelOfParallelism);
                    var tasks = new List<Task>();
                    while (chunks.Count > 1)
                    {
                        semaphore.Wait();

                        progress?.Invoke((1.0 + chunks.Count / pointSetsCount) / 3.0);

                        Console.WriteLine($"[Importer   ] queue: {chunks.Count - 2}, in-flight: {maxLevelOfParallelism - semaphore.CurrentCount}");
                        
                        lock (chunks)
                        {
                            var first = chunks.Dequeue();
                            var second = chunks.Dequeue();

                            tasks.Add(Task.Run(() =>
                            {
                                ct.ThrowIfCancellationRequested();
                                try
                                {
                                    var merged = first.Merge(second, ct);
                                    storage.Add(merged.Id, merged, ct);
                                    lock (chunks) chunks.Enqueue(merged);
                                    Console.WriteLine($"[Importer   ] merged "
                                        + $"{first.Root.Value.Cell} + {second.Root.Value.Cell} -> {merged.Root.Value.Cell} "
                                        + $"({first.Root.Value.PointCountTree} + {second.Root.Value.PointCountTree} -> {merged.Root.Value.PointCountTree})"
                                        );
                                }
                                catch (Exception e)
                                {
                                    Report.Error($"[Importer   ] {e}");
                                }
                                finally
                                {
                                    semaphore.Release();
                                }
                            }, ct));
                        }

                        while (chunks.Count < 2)
                        {
                            var inFlightCount = maxLevelOfParallelism - semaphore.CurrentCount;
                            if (inFlightCount == 0) break;
                            await Task.Delay(TimeSpan.FromSeconds(1.0));
                        }
                    }

                    await Task.WhenAll(tasks);

                    final = chunks.SingleOrDefault();

                    ct.ThrowIfCancellationRequested();
                }
                #endregion

                #region creating LoD
                using (Timing.Do($"[Importer   ] computing LoD data"))
                {
                    var nodeCount = (double)final.Root.Value.CountNodes();
                    var loddedNodesCount = 0L;
                    final = final.GenerateLod(() =>
                    {
                        ct.ThrowIfCancellationRequested();
                        var i = Interlocked.Increment(ref loddedNodesCount);
                        progress?.Invoke(2.0 / 3.0 + (i / nodeCount / 3.0));
                        Console.Write($"[Importer   ] {i}/{nodeCount}\r");
                    },
                    maxLevelOfParallelism,
                    ct
                    );
                    Console.WriteLine();
                }
                ct.ThrowIfCancellationRequested();
                #endregion

                storage.Add(key, final, ct);
                return final;
            }
        }
    }
}
