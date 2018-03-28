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
using System.Linq;
using System.Collections.Generic;
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
            // optionally filter minDist
            if (config.MinDist > 0.0)
            {
                chunks = chunks.Select(x => x.ImmutableFilterSequentialMinDist(config.MinDist));
            }

            // optionally reproject positions and/or estimate normals
            if (config.Reproject != null || config.EstimateNormals != null)
            {
                Chunk map(Chunk x, CancellationToken ct)
                {
                    if (config.Reproject != null)
                    {
                        var ps = config.Reproject(x.Positions);
                        x = x.WithPositions(ps);
                    }

                    if (config.EstimateNormals != null)
                    {
                        var ns = config.EstimateNormals(x.Positions);
                        x = x.WithNormals(ns);
                    }

                    return x;
                }

                chunks = chunks.MapParallel(map, config.MaxDegreeOfParallelism, null, config.CancellationToken);
            }

            // reduce all chunks to single PointSet
            var final = chunks
                .MapReduce(config.WithRandomKey().WithProgressCallback(x => config.ProgressCallback(x * 0.66)))
                ;

            // optionally create LOD data
            if (config.CreateOctreeLod)
            {
                final = final.GenerateLod(config.WithProgressCallback(x => config.ProgressCallback(0.66 + x * 0.34)));
            }

            return final;
        }
    }
}
