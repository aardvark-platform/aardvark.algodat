/*
    Copyright (C) 2006-2024. Aardvark Platform Team. http://github.com/aardvark-platform.
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

namespace Aardvark.Geometry.Points;

/// <summary>
/// Importers for various formats.
/// </summary>
public static partial class PointCloud
{
    private static IEnumerable<Chunk> MergeSmall(int limit, IEnumerable<Chunk> input)
    {
        var current = default(Chunk);
        foreach(var c in input)
        {
            if (c.Count == 0) Report.Warn($"[PointCloud.MergeSmall] empty chunk");

            if(c.Count < limit)
            {
                if (current != null) current = current.Union(c);
                else current = c;

                if (current.Count >= limit)
                {
                    yield return current;
                    current = null;
                }

            }
            else
            {
                yield return c;
            }
        }

        if (current != null)
        {
            yield return current;
        }
    }

    /// <summary>
    /// Imports single chunk.
    /// </summary>
    public static PointSet Chunks(Chunk chunk, ImportConfig config)
        => Chunks([chunk], config);

    /// <summary>
    /// Imports single chunk.
    /// </summary>
    public static PointSet Import(Chunk chunk, ImportConfig config) => Chunks(chunk, config);

    /// <summary>
    /// Imports sequence of chunks.
    /// </summary>
    public static PointSet Chunks(IEnumerable<Chunk> chunks, ImportConfig config)
    {
        config.ProgressCallback(0.0);

        var partIndicesRange = (Range1i?)null;
        var chunkCount = 0;
        chunks = chunks.Do(chunk => 
        {
            if (chunk.HasPartIndices)
            {
                partIndicesRange = PartIndexUtils.ExtendRangeBy(partIndicesRange, chunk.PartIndices);
            }

            if (config.Verbose)
            {
                if (chunk.Count == 0) Report.Warn($"[PointCloud.Chunks] empty chunk");
                Report.Line($"[PointCloud.Chunks] processing chunk {Interlocked.Increment(ref chunkCount)}");
            }
        });

        // reproject positions
        if (config.Reproject != null)
        {
            Chunk map(Chunk x, CancellationToken ct)
            {
                if (config.Reproject != null)
                {
                    var ps = config.Reproject(x.Positions);
                    var y = x.WithPositions(ps);
                    return y;
                }
                else
                {
                    return x;
                }
            }

            chunks = chunks.MapParallel(map, config.MaxDegreeOfParallelism, null, config.CancellationToken);
        }

        // deduplicate points
        chunks = chunks
            .Select(x => x.ImmutableDeduplicate(config.Verbose))
            .Do(chunk =>
             {
                 if (chunk.Count == 0) Report.Warn($"[PointCloud.Chunks] empty chunk");
             })
            ;

        // merge small chunks
        chunks = MergeSmall(config.MaxChunkPointCount, chunks);

        // filter minDist
        if (config.MinDist > 0.0)
        {
            if (config.NormalizePointDensityGlobal)
            {
                var smallestPossibleCellExponent = Fun.Log2(config.MinDist).Ceiling();
                chunks = chunks.Select(x =>
                {
                    if (x.Count == 0) Report.Warn($"[PointCloud.Chunks] empty chunk");
                    var c = new Cell(x.BoundingBox);
                    while (c.Exponent < smallestPossibleCellExponent) c = c.Parent;
                    return x.ImmutableFilterMinDistByCell(c, config.ParseConfig);
                });
            }
            else
            {
                chunks = chunks.Select(x => x.ImmutableFilterSequentialMinDistL1(config.MinDist));
            }
        }

        // merge small chunks
        chunks = MergeSmall(config.MaxChunkPointCount, chunks);

        // EXPERIMENTAL
        //Report.BeginTimed("unmix");
        //chunks = chunks.ImmutableUnmixOutOfCore(@"T:\tmp", 1, config);
        //Report.End();

#if DEBUG
#if false
        // store chunks before map/reduce
        var debugChunkIndex = 0L;
        var debugChunkDir = new DirectoryInfo(@"W:\tmp\debugChunks");
        if (!debugChunkDir.Exists) debugChunkDir.Create();
        chunks = chunks
            .Do(chunk =>
            {
                var buffer = chunk.ToGenericChunk().Data.DurableEncode(Durable.Primitives.DurableMap, gzipped: false);
                var filename = Path.Combine(debugChunkDir.FullName, $"chunk_{debugChunkIndex++:00000}.dur");
                File.WriteAllBytes(filename, buffer);
                if (chunk.Count == 0) Report.Warn($"[PointCloud.Chunks] empty chunk");
            })
            ;
#endif
#endif
        // reduce all chunks to single PointSet
        if (config.Verbose) Report.BeginTimed("map/reduce");
        PointSet final = chunks
            .MapReduce(config.WithRandomKey().WithProgressCallback(x => config.ProgressCallback(0.01 + x * 0.65)))
            ;
        if (config.Verbose) Report.EndTimed();

        // create LOD data
        if (config.Verbose) Report.BeginTimed("generate lod");
        final = final.GenerateLod(config.WithRandomKey().WithProgressCallback(x => config.ProgressCallback(0.66 + x * 0.34)));
        if (final.Root.Value != null && final.Root.Value.Id != Guid.Empty && config.Storage?.GetPointCloudNode(final.Root.Value.Id) == null) throw new InvalidOperationException("Invariant 4d633e55-bf84-45d7-b9c3-c534a799242e.");
        if (config.Verbose) Report.End();

        // create final point set with specified key (or random key when no key is specified)
        var key = config.Key ?? Guid.NewGuid().ToString();
        final = new PointSet(
            storage: config.Storage ?? throw new Exception($"No storage specified. Error 5b4ebfec-d418-4ddc-9c2f-646d270cf78c."),
            pointSetId: key,
            rootCellId: final.Root.Value!.Id,
            splitLimit: config.OctreeSplitLimit
            );
        config.Storage.Add(key, final);
        return final;
    }

    /// <summary>
    /// Imports sequence of chunks.
    /// </summary>
    public static PointSet Import(IEnumerable<Chunk> chunks, ImportConfig config) => Chunks(chunks, config);
}
