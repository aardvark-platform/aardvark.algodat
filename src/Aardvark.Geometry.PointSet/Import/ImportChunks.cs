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
using Aardvark.Base;
using Aardvark.Data.Points;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// Importers for various formats.
    /// </summary>
    public static partial class PointCloud
    {

        private static IEnumerable<Chunk> MergeSmall(int limit, IEnumerable<Chunk> input)
        {
            Chunk? current = null;
            foreach(var c in input)
            {
                if(c.Count < limit)
                {
                    if (current.HasValue) current = current.Value.Union(c);
                    else current = c;

                    if (current.Value.Count >= limit)
                    {
                        yield return current.Value;
                        current = null;
                    }

                }
                else
                {
                    yield return c;
                }
            }

            if (current.HasValue)
            {
                yield return current.Value;
                current = null;
            }
        }

        /// <summary>
        /// Imports single chunk.
        /// </summary>
        public static PointSet Chunks(Chunk chunk, ImportConfig config)
            => Chunks(new[] { chunk }, config);
        
        /// <summary>
        /// Imports sequence of chunks.
        /// </summary>
        public static PointSet Chunks(IEnumerable<Chunk> chunks, ImportConfig config)
        {
            chunks = MergeSmall(config.OctreeSplitLimit, chunks);


            config?.ProgressCallback(0.0);

            // optionally filter minDist
            if (config.MinDist > 0.0)
            {
                if (config.NormalizePointDensityGlobal)
                {
                    chunks = chunks.Select(x => x.ImmutableFilterMinDistByCell(new Cell(x.BoundingBox), config.ParseConfig));
                }
                else
                {
                    chunks = chunks.Select(x => x.ImmutableFilterSequentialMinDistL1(config.MinDist));
                }
            }

            //Report.BeginTimed("unmix");
            //chunks = chunks.ImmutableUnmixOutOfCore(@"T:\tmp", 1, config);
            //Report.End();

            // optionally deduplicate points
            if (config.DeduplicateChunks)
            {
                chunks = chunks.Select(x => x.ImmutableDeduplicate());
            }

            // optionally reproject positions and/or estimate normals
            if (config.Reproject != null)
            {
                Chunk map(Chunk x, CancellationToken ct)
                {
                    if (config.Reproject != null)
                    {
                        var ps = config.Reproject(x.Positions);
                        x = x.WithPositions(ps);
                    }

                    //if (config.EstimateNormals != null)
                    //{
                    //    var ns = config.EstimateNormals(x.Positions);
                    //    x = x.WithNormals(ns);
                    //}

                    return x;
                }

                chunks = chunks.MapParallel(map, config.MaxDegreeOfParallelism, null, config.CancellationToken);
            }

            //var foo = chunks.ToArray();

            // reduce all chunks to single PointSet
            Report.BeginTimed("map/reduce");
            var final = chunks
                .MapReduce(config.WithRandomKey().WithProgressCallback(x => config.ProgressCallback(0.01 + x * 0.65)))
                ;
            Report.EndTimed();

            // create LOD data
            Report.BeginTimed("generate lod");
            final = final.GenerateLod(config.WithRandomKey().WithProgressCallback(x => config.ProgressCallback(0.66 + x * 0.34)));
            if (config.Storage.GetPointCloudNode(final.Root.Value.Id) == null) throw new InvalidOperationException("Invariant 4d633e55-bf84-45d7-b9c3-c534a799242e.");
            Report.End();

            // create final point set with specified key (or random key when no key is specified)
            var key = config.Key ?? Guid.NewGuid().ToString();
#pragma warning disable CS0618 // Type or member is obsolete
            final = new PointSet(config.Storage, key, final?.Root?.Value?.Id, config.OctreeSplitLimit);
#pragma warning restore CS0618 // Type or member is obsolete
            config.Storage.Add(key, final);

            return final;
        }
    }
}
