﻿/*
    Copyright (C) 2006-2023. Aardvark Platform Team. http://github.com/aardvark-platform.
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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// </summary>
    public static partial class ImportExtensions
    {
        /// <summary>
        /// Maps a sequence of point chunks to point sets, which are then reduced to one single point set.
        /// </summary>
        public static PointSet MapReduce(this IEnumerable<Chunk> chunks, ImportConfig config)
        {
            var key = config.Key ?? Guid.NewGuid().ToString();
            var totalChunkCount = 0;
            var totalPointCountInChunks = 0L;
            Action<double> progress = x => config.ProgressCallback(x * 0.5);
            
            #region MAP: create one PointSet for each chunk

            var pointsets = chunks
                .Where(chunk => chunk.Count > 0)
                .MapParallel((chunk, ct2) =>
                {
                    Interlocked.Add(ref totalPointCountInChunks, chunk.Count);
                    progress(Math.Sqrt(1.0 - 1.0 / Interlocked.Increment(ref totalChunkCount)));

                    var builder = InMemoryPointSet.Build(chunk, config.OctreeSplitLimit);
                    var root = builder.ToPointSetNode(config.Storage, isTemporaryImportNode: true);
                    var id = $"Aardvark.Geometry.PointSet.{Guid.NewGuid()}.json";
                    var pointSet = new PointSet(config.Storage, id, root.Id, config.OctreeSplitLimit);

#if DEBUG
                    if (config.ParseConfig.EnabledProperties.PartIndices)
                    {
                        if (!chunk.HasPartIndices) throw new Exception("Invariant 04c62ea7-287b-473f-a3e9-6b5451222e27.");
                    }
#endif

                    return pointSet;
                },
                config.MaxDegreeOfParallelism, null, config.CancellationToken
                )
                .ToList()
                ;
            ;

            if (config.Verbose)
            {
                Console.WriteLine($"[MapReduce] pointsets              : {pointsets.Count:N0}");
                Console.WriteLine($"[MapReduce] totalPointCountInChunks: {totalPointCountInChunks:N0}");
            }

            #endregion

            #region REDUCE: pairwise octree merge until a single (final) octree remains

            progress = x => config.ProgressCallback(0.5 + x * 0.5);
            var i = 0;
            var fractionalProgress = new Dictionary<int, double>();

            var totalPointsToMerge = pointsets.Sum(x => x.PointCount);
            if (config.Verbose)
            {
                Console.WriteLine($"[MapReduce] totalPointsToMerge     : {totalPointsToMerge:N0}");
            }

            var totalPointSetsCount = pointsets.Count;
            if (totalPointSetsCount == 0)
            {
                return PointSet.Empty;
            }

            var doneCount = 0;
            var parts = new HashSet<PointSet>(pointsets);
            var final = pointsets.MapReduceParallel((first, second, ct2) =>
            {
                if (first .Root.Value == null) throw new Exception($"Expected first octree not to be null. Error c1ac4063-efaa-4c92-879c-7925509adee4.");
                if (second.Root.Value == null) throw new Exception($"Expected second octree not to be null. Error 65289b58-2c36-46dd-91a6-81b42554d625.");

                lock (parts)
                {
                    if (!parts.Remove(first)) throw new InvalidOperationException("map reduce error");
                    if (!parts.Remove(second)) throw new InvalidOperationException("map reduce error");
                }
                
                var id = Interlocked.Increment(ref i);
                var firstPlusSecondPointCount = first.PointCount + second.PointCount;

                var lastN = 0L;
                var merged = first.Merge(second,
                    n =>
                    {
                        //Console.WriteLine($"[MERGE CALLBACK][{id}] {n:N0}");
                        if (n > lastN)
                        {
                            lastN = n;
                            var p = 0.0;
                            lock (fractionalProgress)
                            {
                                fractionalProgress[id] = n / (double)firstPlusSecondPointCount;
                                p = 1.0 / (totalPointSetsCount - (doneCount + fractionalProgress.Values.Sum()));
                            }
                            progress(p);
                        }
                    },
                    config.WithCancellationToken(ct2)
                    );

                lock (fractionalProgress)
                {
                    fractionalProgress.Remove(id);
                    Interlocked.Increment(ref doneCount);
                }

                if (merged.Root.Value == null) throw new Exception($"Expected merged octree not to be null. Error b49cf692-2c1f-4067-b4cc-2960783fe141.");

                //Console.WriteLine($"[MERGE CALLBACK][{id}] {(first.PointCount + second.PointCount) / (double)totalPointsToMerge,7:N3}");

                lock (parts)
                {
                    parts.Add(merged);
                }

                config.Storage.Add(merged.Id, merged);
                if (config.Verbose) Console.WriteLine($"[MapReduce] merged "
                    + $"{formatCell(first.Root.Value.Cell)} + {formatCell(second.Root.Value.Cell)} -> {formatCell(merged.Root.Value.Cell)} "
                    + $"({first.Root.Value.PointCountTree:N0} + {second.Root.Value.PointCountTree:N0} -> {merged.Root.Value.PointCountTree:N0})"
                    );

                if (merged.Root.Value.PointCountTree == 0) throw new InvalidOperationException();
                return merged;
            },
            config.MaxDegreeOfParallelism
            );
            if (config.Verbose)
            {
                Console.WriteLine($"[MapReduce] everything merged");
            }

            config.CancellationToken.ThrowIfCancellationRequested();

            #endregion

            config.Storage.Add(key, final);
            config.ProgressCallback(1.0);
            return final;

            static string formatCell(Cell c) => c.IsCenteredAtOrigin ? $"[centered, {c.Exponent}]" : c.ToString();
        }
    }
}
