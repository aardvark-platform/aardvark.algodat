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
using Aardvark.Base;

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
            var progress = config.Progress ?? ProgressReporter.None;
            var totalPointCountInChunks = 0L;

            #region Setup
            var pr1 = Progress.Reporter();
            var pr2 = Progress.Reporter();
            (
                0.33 * pr1.Normalize() +
                0.64 * pr2.Normalize() 
            )
            .Normalize()
            .Subscribe(progress.Report)
            ;
            #endregion

            #region MAP: create one PointSet for each chunk
            var pointsets = chunks
                .MapParallel((chunk, ct2) =>
                {
                    totalPointCountInChunks += chunk.Count;
                    
                    var builder = InMemoryPointSet.Build(chunk, config.OctreeSplitLimit);
                    var root = builder.ToPointSetCell(config.Storage, ct: ct2);
                    var id = $"Aardvark.Geometry.PointSet.{Guid.NewGuid()}.json";
                    var pointSet = new PointSet(config.Storage, id, root.Id, config.OctreeSplitLimit);
                    //pr1.Report(chunk.SequenceNumber, chunk.SequenceLength);
                    return pointSet;
                },
                config.MaxDegreeOfParallelism, null, config.CancellationToken
                )
                .ToList()
                ;
            ;
            pr1.ReportFinished();
            if (config.Verbose)
            {
                Console.WriteLine($"[MapReduce] pointsets              : {pointsets.Count}");
                Console.WriteLine($"[MapReduce] totalPointCountInChunks: {totalPointCountInChunks}");
            }
            #endregion

            #region REDUCE: pairwise octree merge until a single (final) octree remains
            var totalPointsToMerge = pointsets.Sum(x => x.PointCount);
            if (config.Verbose) Console.WriteLine($"[MapReduce] totalPointsToMerge: {totalPointsToMerge}");

            var totalPointSetsCount = pointsets.Count;
            if (totalPointSetsCount == 0) throw new Exception("woohoo");
            var reduceStepsCount = 0;
            var final = pointsets.MapReduceParallel((first, second, ct2) =>
            {
                var merged = first.Merge(second, ct2);
                config.Storage.Add(merged.Id, merged, ct2);
                if (config.Verbose) Console.WriteLine($"[MapReduce] merged "
                    + $"{first.Root.Value.Cell} + {second.Root.Value.Cell} -> {merged.Root.Value.Cell} "
                    + $"({first.Root.Value.PointCountTree} + {second.Root.Value.PointCountTree} -> {merged.Root.Value.PointCountTree})"
                    );
                pr2.Report(Interlocked.Increment(ref reduceStepsCount), totalPointSetsCount);
                return merged;
            },
            config.MaxDegreeOfParallelism
            );
            if (config.Verbose)
            {
                Console.WriteLine($"[MapReduce] everything merged");
            }
            pr2.ReportFinished();
            config.CancellationToken.ThrowIfCancellationRequested();
            #endregion

            config.Storage.Add(config.Key, final, config.CancellationToken);
            return final;
        }
    }
}
