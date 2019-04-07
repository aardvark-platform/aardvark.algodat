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
            config?.ProgressCallback(0.0);

            // optionally filter minDist
            if (config.MinDist > 0.0)
            {
                chunks = chunks.Select(x => x.ImmutableFilterSequentialMinDistL1(config.MinDist));
            }

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
                    return x;
                }

                chunks = chunks.MapParallel(map, config.MaxDegreeOfParallelism, null, config.CancellationToken);
            }

            // reduce all chunks to single PointSet
            var final = chunks
                .MapReduce(config.WithRandomKey().WithProgressCallback(x => config.ProgressCallback(0.01 + x * 0.65)))
                ;

            // optionally create LOD data
            if (config.CreateOctreeLod)
            {
                final = final.GenerateLod(config.WithRandomKey().WithProgressCallback(x => config.ProgressCallback(0.66 + x * 0.34)));
            }

            // create final point set with specified key (or random key when no key is specified)
            var key = config.Key ?? Guid.NewGuid().ToString();
            final = new PointSet(config.Storage, key, final?.Root?.Value?.Id, config.OctreeSplitLimit);
            config.Storage.Add(key, final, config.CancellationToken);

            return final;
        }
    }
}
